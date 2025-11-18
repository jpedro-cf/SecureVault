import z from 'zod'
import { Form } from '../ui/form'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { DropZone, DropZoneContent } from '../ui/drop-zone'
import { cn, formatFileSize } from '@/lib/utils'
import { CloudCheckIcon, FileInput, X } from 'lucide-react'
import { Button } from '../ui/button'
import { useFilesUpload } from '@/api/files/upload'
import { useState } from 'react'

const uploadFilesSchema = z.object({
    parentId: z.string().min(1).optional(),
    files: z
        .array(
            z.object({
                id: z.string().min(1),
                content: z.instanceof(File),
            })
        )
        .min(1),
})

export type UploadFilesSchema = z.infer<typeof uploadFilesSchema>

interface FileUploadState {
    status: 'uploading' | 'error' | 'done'
    progress: number
}

interface Props {
    parentId?: string
    onComplete: () => void
}
export function UploadFilesForm({ parentId, onComplete }: Props) {
    const [uploads, setUploads] = useState<Record<string, FileUploadState>>({})

    const form = useForm<UploadFilesSchema>({
        resolver: zodResolver(uploadFilesSchema),
        defaultValues: {
            parentId,
            files: [],
        },
    })

    const { mutate, isPending } = useFilesUpload({
        onProgress: (id, newProgress) => {
            setUploads((prev) => {
                return {
                    ...prev,
                    [id]: {
                        status: newProgress < 100 ? 'uploading' : 'done',
                        progress: newProgress,
                    },
                }
            })
        },
        onError: (id) => {
            setUploads((prev) => {
                const item = prev[id]
                return {
                    ...prev,
                    [id]: {
                        ...item,
                        status: 'error',
                    },
                }
            })
        },
        onSuccess: handleDelete,
    })

    function handleSubmit(data: UploadFilesSchema) {
        mutate(data, {
            onSuccess: () => {
                if (form.getValues('files').length == 0) {
                    onComplete()
                }
            },
        })
    }

    function handleDrop(files: File[]) {
        const newFiles = files.map((f) => {
            const id = crypto.randomUUID()
            setUploads((prev) => ({
                ...prev,
                [id]: {
                    progress: 0,
                    status: 'uploading',
                },
            }))

            return {
                content: f,
                id,
            }
        })

        form.setValue('files', [...form.getValues('files'), ...newFiles])
    }

    function handleDelete(id: string) {
        form.setValue(
            'files',
            form.getValues('files').filter((f) => f.id != id)
        )
        setUploads((prev) => {
            const updated = { ...prev }
            delete updated[id]
            return updated
        })
    }

    const files = form.watch('files')

    return (
        <Form {...form}>
            <form
                onSubmit={form.handleSubmit(handleSubmit)}
                className="flex flex-col gap-3"
            >
                <DropZone onDrop={handleDrop}>
                    <DropZoneContent />
                </DropZone>
                <div className="max-h-[calc(50vh-32px)] overflow-y-scroll space-y-2">
                    {files.map((file) => (
                        <div
                            key={file.id}
                            className={cn(
                                'overflow-hidden relative flex items-start justify-between text-slate-100 bg-blue-600/20 rounded-lg p-3 border border-blue-500/20',
                                uploads[file.id].status == 'error'
                                    ? 'bg-red-800/50 border-red-800'
                                    : '',
                                uploads[file.id].status == 'done'
                                    ? 'bg-green-800/50 border-green-800'
                                    : ''
                            )}
                        >
                            {uploads[file.id].status == 'uploading' && (
                                <div
                                    className={cn(
                                        'absolute bottom-0 left-0 h-1 bg-blue-400 transition-all'
                                    )}
                                    style={{
                                        width: `${uploads[file.id].progress}%`,
                                    }}
                                ></div>
                            )}
                            <div className="flex items-start gap-3">
                                <FileInput />
                                <div>
                                    <span className="font-semibold block">
                                        {file.content.name}
                                    </span>
                                    <span className="text-sm">
                                        {formatFileSize(file.content.size)}
                                    </span>
                                </div>
                            </div>
                            <Button
                                variant={'ghost'}
                                type="button"
                                size={'icon-sm'}
                                onClick={() => handleDelete(file.id)}
                                className="hover:bg-red-900"
                                disabled={isPending}
                            >
                                <X />
                            </Button>
                        </div>
                    ))}
                </div>
                <Button type="submit" variant={'primary'} disabled={isPending}>
                    Upload <CloudCheckIcon />
                </Button>
            </form>
        </Form>
    )
}
