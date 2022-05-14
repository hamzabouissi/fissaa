using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using CSharpFunctionalExtensions;

namespace fissaa.CloudProvidersServices;

public class AwsNetworkService
{
    private readonly string _baseDomain;
    private readonly string _escapedBaseDomain;
    public string DomainCertificateStackName=>$"{_escapedBaseDomain}-certificate-stack";
    public string AlbStackName => $"{_escapedBaseDomain}-alb-stack";

    public string NetworkStackName => $"Network-Stack";
    public string VpcStackName => "Vpc-Stack";
    public readonly RegionEndpoint Region = RegionEndpoint.USEast1;
    public readonly AmazonCloudFormationClient ClientCformation;
    private readonly AwsUtilFunctions _awsUtilFunctions;
    
    public AwsNetworkService(string awsSecretKey,string awsAccessKey, string domainName)
    {
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        ClientCformation = new AmazonCloudFormationClient(auth,Region);
        _awsUtilFunctions = new AwsUtilFunctions(awsSecretKey, awsAccessKey);
        var domainName1 = domainName;
        _baseDomain = string.Join(".",domainName1.Split(".")[^2..]);
        _escapedBaseDomain = string.Join("-",domainName1.Split(".")[^2..]);
    }

    public async Task<Result> CreateVpc()
    {
        var cloudFile =
            await _awsUtilFunctions.ExtractTextFromRemoteFile("https://fissaa-cli.s3.amazonaws.com/test/vpc.yml");
        try
        {
            await ClientCformation.CreateStackAsync(new CreateStackRequest
            {
                OnFailure = OnFailure.DELETE,
                StackName = VpcStackName,
                TemplateBody = cloudFile,
                TimeoutInMinutes = 30,
                Tags = new List<Tag>()
                {
                    new()
                    {
                        Key = "app-domain",
                        Value = _baseDomain
                    }
                }
            });
            await _awsUtilFunctions.WaitUntilStackCreatedOrDeleted(VpcStackName);

        }
        catch (AlreadyExistsException)
        {
        }
        var status = await _awsUtilFunctions.GetStackStatus(VpcStackName);
        return _awsUtilFunctions.StackStatusIsSuccessfull(status) ? Result.Success(status): Result.Failure("creating vpc failed") ;
    }
    public async Task<Result> Create()
    {
       
        var cloudFile =
            await _awsUtilFunctions.ExtractTextFromRemoteFile("https://fissaa-cli.s3.amazonaws.com/test/network.yml");
       
        
        try
        {
            await ClientCformation.CreateStackAsync(new CreateStackRequest
            {
                OnFailure = OnFailure.DELETE,
                StackName = NetworkStackName,
                TemplateBody = cloudFile,
                TimeoutInMinutes = 30,
                Tags = new List<Tag>()
                {
                    new()
                    {
                        Key = "app-domain",
                        Value = _baseDomain
                    }
                }
            });
            await _awsUtilFunctions.WaitUntilStackCreatedOrDeleted(NetworkStackName);

        }
        catch (AlreadyExistsException)
        {
        }
        var status = await _awsUtilFunctions.GetStackStatus(NetworkStackName);
        return _awsUtilFunctions.StackStatusIsSuccessfull(status) ? Result.Success(status): Result.Failure("creating network failed") ;

    }
    
    public async Task Destroy()
    {
        await _awsUtilFunctions.DeleteStack(NetworkStackName);
    }

    public async Task<Result> CreateAlb()
    {
        var cloudFile =
            await _awsUtilFunctions.ExtractTextFromRemoteFile("https://fissaa-cli.s3.amazonaws.com/test/alb.yml");
        var parameters = new List<Parameter>()
        {
            new ()
            {
                ParameterKey = "DomainCertificateStackName",
                ParameterValue = DomainCertificateStackName,
            }
        };
        
        try
        {
            await ClientCformation.CreateStackAsync(new CreateStackRequest
            {
                OnFailure = OnFailure.DELETE,
                Parameters = parameters,
                StackName = AlbStackName,
                Capabilities = new List<string>()
                {
                    "CAPABILITY_NAMED_IAM"
                },
                TemplateBody = cloudFile,
                TimeoutInMinutes = 30,
                Tags = new List<Tag>()
                {
                    new()
                    {
                        Key = "app-domain",
                        Value = _baseDomain
                    }
                }
            });
            await _awsUtilFunctions.WaitUntilStackCreatedOrDeleted(AlbStackName);
        }
        catch (AlreadyExistsException)
        {
        }
        var status = await _awsUtilFunctions.GetStackStatus(AlbStackName);
        return _awsUtilFunctions.StackStatusIsSuccessfull(status) ? Result.Success(status): Result.Failure("creating alb failed") ;
    }

    public async Task DestroyLoadBalancer()
    {
        await _awsUtilFunctions.DeleteStack(AlbStackName);
    }
}