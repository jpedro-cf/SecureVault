import type { UploadFilesSchema } from '@/components/files/UploadFilesForm'
import { useKeys } from '@/hooks/use-keys'
import { Encoding } from '@/lib/encoding'
import { Encryption } from '@/lib/encryption'
import type { FileItem, FolderItem, ItemResponse } from '@/types/items'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { AxiosError } from 'axios'
import { toast } from 'sonner'
import { api } from '../axios'
import type { Folder } from '@/types/folders'
import { useAuth } from '@/hooks/use-auth'
import { config } from '@/config/config'

interface InitiateUpload {
    fileName: string
    fileSize: number
    contentType: string
    encryptedKey: string
    keyEncryptedByRoot: string
    parentFolderId?: string
}

interface InitiateUploadResponse {
    fileId: string
    uploadId: string
    key: string
    urls: PresignedPart[]
}

interface UploadParts extends InitiateUploadResponse {
    fileContent: Uint8Array<ArrayBuffer>
}

interface PresignedPart {
    partNumber: number
    url: string
}

interface CompletedPart {
    partNumber: number
    ETag: string
}

interface CompleteUpload {
    fileId: string
    uploadId: string
    key: string
    parts: CompletedPart[]
}

interface Props {
    onProgress: (id: string, newProgress: number) => void
    onError: (id: string) => void
    onSuccess: (id: string) => void
}
export function useFilesUpload({ onProgress, onError, onSuccess }: Props) {
    const queryClient = useQueryClient()
    const { rootKey, folderKeys, setFileKey } = useKeys()
    const { storageUsage, updateStorageUsage } = useAuth()

    const usedStorage = Object.values(storageUsage!).reduce(
        (prev, curr) => curr + prev,
        0
    )

    async function request(data: UploadFilesSchema) {
        if (!rootKey || (data.parentId && !folderKeys[data.parentId])) {
            throw new Error(
                'One of the encryption keys for this file was not found.'
            )
        }

        const res: FileItem[] = []

        for (const file of data.files) {
            try {
                if (usedStorage >= config.TOTAL_STORAGE) {
                    onError(file.id)
                    return res
                }

                const fileEncryptionKey = Encryption.generateRandomSecret()
                const parentKey = data.parentId
                    ? folderKeys[data.parentId]
                    : rootKey

                const { combined: encryptedName } = await Encryption.encrypt({
                    data: Encoding.textToUint8Array(file.content.name),
                    key: fileEncryptionKey,
                })

                const { combined: encryptedKey } = await Encryption.encrypt({
                    data: fileEncryptionKey,
                    key: parentKey,
                })

                const { combined: keyEncryptedByRoot } =
                    await Encryption.encrypt({
                        data: fileEncryptionKey,
                        key: rootKey,
                    })

                const { combined: encryptedFileContent } =
                    await Encryption.encrypt({
                        data: new Uint8Array(await file.content.arrayBuffer()),
                        key: fileEncryptionKey,
                    })

                const initial = await initiateUpload({
                    contentType: file.content.type,
                    fileName: Encoding.uint8ArrayToBase64(encryptedName),
                    fileSize: file.content.size,
                    parentFolderId: data.parentId,
                    encryptedKey: Encoding.uint8ArrayToBase64(encryptedKey),
                    keyEncryptedByRoot:
                        Encoding.uint8ArrayToBase64(keyEncryptedByRoot),
                })

                onProgress(file.id, 33)

                const uploadedParts = await uploadParts(
                    { ...initial, fileContent: encryptedFileContent },
                    (progress) => onProgress(file.id, progress)
                )

                const uploadedFile = await completeUpload({
                    fileId: initial.fileId,
                    key: initial.key,
                    parts: uploadedParts,
                    uploadId: initial.uploadId,
                })

                onProgress(file.id, 100)

                setFileKey(uploadedFile.id, fileEncryptionKey)

                const item: FileItem = {
                    id: uploadedFile.id,
                    name: file.content.name,
                    size: uploadedFile.size!,
                    contentType: uploadedFile.contentType!,
                    createdAt: uploadedFile.createdAt,
                    parentId: data.parentId,
                    key: fileEncryptionKey,
                }

                res.push(item)

                updateStorageUsage(
                    uploadedFile.contentType!,
                    encryptedFileContent.byteLength
                )
                onSuccess(file.id)
            } catch (error) {
                onError(file.id)
            }
        }

        return res
    }

    return useMutation({
        mutationFn: request,
        onError: (e: AxiosError<{ detail?: string }>) => {
            toast.warning(
                e.response?.data.detail ??
                    'An error occured while performing this operation.'
            )
        },
        onSuccess: (data, variables) => {
            const queryKey = variables.parentId
                ? ['folder', { id: variables.parentId }]
                : ['items']

            const previous = queryClient.getQueryData(queryKey)

            if (!variables.parentId) {
                const previousItems = previous as (FolderItem | FileItem)[]
                queryClient.setQueryData(queryKey, [...previousItems, ...data])
                return
            }
            const previousFolder = previous as Folder
            queryClient.setQueryData(queryKey, {
                ...previousFolder,
                children: [...previousFolder.children, ...data],
            })
        },
    })
}
async function initiateUpload(
    fileData: InitiateUpload
): Promise<InitiateUploadResponse> {
    return (await api.post(`/files/upload`, fileData)).data
}

async function uploadParts(
    data: UploadParts,
    callback: (progress: number) => void
): Promise<CompletedPart[]> {
    const completedParts: CompletedPart[] = []
    const promises: Promise<void>[] = []

    const chunkSize = Math.ceil(data.fileContent.byteLength / data.urls.length)
    let offset = 0
    data.urls.forEach((u) => {
        const chunk = data.fileContent.slice(offset, offset + chunkSize)

        const promise = api
            .put(u.url, chunk, {
                withCredentials: false,
            })
            .then((res) => {
                const etag = res.headers['ETag'] || res.headers['etag']
                completedParts.push({ ETag: etag, partNumber: u.partNumber })

                callback(33 + (completedParts.length / data.urls.length) * 33)
            })

        offset += chunkSize

        promises.push(promise)
    })

    await Promise.all(promises)

    return completedParts
}

async function completeUpload(data: CompleteUpload): Promise<ItemResponse> {
    return (await api.post(`/files/${data.fileId}/complete-upload`, data)).data
}
