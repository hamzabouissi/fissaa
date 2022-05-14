using Amazon;
using CSharpFunctionalExtensions;
using fissaa.CloudProvidersServices;
using fissaa.commands.Templates;

namespace fissaa.TemplatesServices;

public class GhostTemplateService
{
    private readonly FileUtilFunctions _fileUtilFunctions;
    private readonly string _secretKey;
    private readonly string _accessKey;
    private readonly AwsUtilFunctions _awsUtilFunctions;
    private readonly string _domainName;
    private readonly AwsEcsService _ecsService;
    public readonly RegionEndpoint Region = RegionEndpoint.USEast1;


    public GhostTemplateService(string awsSecretKey,string awsAccessKey, string domainName)
    {
        _secretKey = awsSecretKey;
        _accessKey = awsAccessKey;
        _domainName = domainName;
        
        _ecsService = new AwsEcsService(_secretKey,awsAccessKey,_domainName);
        _awsUtilFunctions = new AwsUtilFunctions(awsSecretKey, awsAccessKey);
        _fileUtilFunctions = new FileUtilFunctions();

    }
    public async Task<Result> CreateGhost(string s3BucketName, DatabaseAuth databaseAuth, MailAuth mailAuth)
    {
        // Create  and Host an Env File
        _fileUtilFunctions.CreateEnvFile(_accessKey,_secretKey, Region.SystemName,_domainName,databaseAuth,s3BucketName,mailAuth);
        await _awsUtilFunctions.UploadS3File(File.OpenRead("env.txt"),s3BucketName,"env_file.env");
        _fileUtilFunctions.DeleteEnvFile();
        // deployment
        return await _ecsService.Create(ContainerTemplate.Ghost, string.Empty, false,$"{s3BucketName}/env_file.env");
    }
}