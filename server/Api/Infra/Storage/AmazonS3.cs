using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using EncryptionApp.Api.Dtos.Files;
using File = EncryptionApp.Api.Entities.File;

namespace EncryptionApp.Api.Infra.Storage;

public class AmazonS3
{

    public readonly string _bucketName;
    private readonly IHostEnvironment _env;
    private readonly AmazonS3Client _client;
    private readonly string? _endpoint;

    private readonly ILogger<AmazonS3> _logger;

    public AmazonS3(IHostEnvironment env, ILogger<AmazonS3> logger)
    {
        _bucketName = Environment.GetEnvironmentVariable("AWS_BUCKET_NAME") ?? "";
        _env = env;
        _endpoint = Environment.GetEnvironmentVariable("AWS_ENDPOINT");
        _logger = logger;
        
        var credentials = new BasicAWSCredentials(
            Environment.GetEnvironmentVariable("AWS_ACCESS") ?? "", 
            Environment.GetEnvironmentVariable("AWS_SECRET") ?? "");

        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(Environment.GetEnvironmentVariable("AWS_REGION")),
            ForcePathStyle = true,
            UseHttp = true,
            AuthenticationRegion = Environment.GetEnvironmentVariable("AWS_REGION"),
        };
        
        if (!string.IsNullOrEmpty(_endpoint))
        {
            config.ServiceURL = _endpoint;
        }
        
        _client = new AmazonS3Client(credentials, config);
    }

    public async Task<InitiateUploadResponse> InitiateMultiPartUpload(File file)
    {
        try
        {
            var initRequest = new InitiateMultipartUploadRequest
            {
                BucketName = _bucketName,
                Key = file.StorageKey,
            };
            // initRequest.Metadata.Add("file_id", file.Id.ToString());
            
            var initResponse = await _client.InitiateMultipartUploadAsync(initRequest);
            var presignedUrls = new List<PresignedPartUrl>();
            
            const int chunkSize = 50 * 1024 * 1024; // 50mb
            int totalParts = (int)Math.Ceiling((double)file.Size / chunkSize);
            
            for (var i = 1; i <= totalParts; i++)
            {
                var urlRequest = new GetPreSignedUrlRequest
                {
                    BucketName = _bucketName,
                    Key = file.StorageKey,
                    Verb = HttpVerb.PUT,
                    Protocol = _env.IsDevelopment() ? Protocol.HTTP : Protocol.HTTPS,
                    PartNumber = i,
                    UploadId = initResponse.UploadId,
                    Expires = DateTime.UtcNow.AddMinutes(30),
                };

                var url = await _client.GetPreSignedURLAsync(urlRequest);
                presignedUrls.Add(new PresignedPartUrl(i, url));
            }
            
            return new InitiateUploadResponse(file.Id.ToString(), initResponse.UploadId, file.StorageKey, presignedUrls);
        }
        catch (Exception e)
        {
            _logger.LogError($"Error while initiating upload: {e}");
            throw;
        }
    }

    public async Task<UploadCompletedResponse> CompleteMultiPartUpload(CompleteUploadRequest data)
    {
        try
        {
            var completeRequest = new CompleteMultipartUploadRequest
            {
                BucketName = _bucketName,
                Key = data.Key,
                UploadId = data.UploadId,
                PartETags = data.Parts.Select(p => new PartETag(p.PartNumber, p.ETag)).ToList()
            };
            
            var response = await _client.CompleteMultipartUploadAsync(completeRequest);

            return new UploadCompletedResponse(response.Key);

        }
        catch (Exception e)
        {
            _logger.LogError($"Error while completing upload: {e}");
            throw;
        }
    }

    public async Task<List<UploadPart>> ListUploadParts(string key, string uploadId)
    {
        try
        {
            var data = await _client.ListPartsAsync(_bucketName, key, uploadId);
            
            return data.Parts
                .Select(p => new UploadPart(p.LastModified, p.Size, p.PartNumber, p.ETag))
                .ToList();
        }
        catch (Exception e)
        {
            _logger.LogError($"Error while listing upload parts: {e}");
            throw;
        }
    }

    public async Task AbortMultiPartUpload(string key, string uploadId)
    {
        try
        {
            await _client.AbortMultipartUploadAsync(_bucketName, key, uploadId);
        }
        catch (Exception e)
        {
            _logger.LogError($"Error while aborting upload: {e}");
            throw;
        }
    }

    public async Task<string> GeneratePresignedUrl(string key)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Protocol = _env.IsDevelopment() ? Protocol.HTTP : Protocol.HTTPS,
                Expires = DateTime.UtcNow.AddHours(3),
            };

            return await _client.GetPreSignedURLAsync(request);

        }
        catch (Exception e)
        {
            _logger.LogError($"Error while generating signed url: {e}");
            throw;
        }
    }

    public async Task<GetObjectMetadataResponse> GetObjectMetadata(string key)
    {
        return await _client.GetObjectMetadataAsync(_bucketName, key);
    }

    public async Task DeleteObject(string key)
    {
        try
        {
            await _client.DeleteObjectAsync(_bucketName, key);
        }
        catch (Exception e)
        {
            _logger.LogError($"Error while deleting object '{key}': {e}");
            throw;
        }
    }
}