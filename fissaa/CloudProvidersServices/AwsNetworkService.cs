using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using CSharpFunctionalExtensions;

namespace fissaa;

public class AwsNetworkService
{
    private readonly string DomainName;
    private readonly string BaseDomain;
    private readonly string EscapedBaseDomain;
    public string DomainCertificateStackName=>$"{EscapedBaseDomain}-certificate-stack";
    public string NetworkStackName => $"{EscapedBaseDomain}-network-stack";
    public readonly RegionEndpoint Region = RegionEndpoint.USEast1;
    public readonly AmazonCloudFormationClient ClientCformation;
    public readonly AwsUtilFunctions awsUtilFunctions;
    
    public AwsNetworkService(string awsSecretKey,string awsAccessKey, string domainName)
    {
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        ClientCformation = new AmazonCloudFormationClient(auth,Region);
        awsUtilFunctions = new AwsUtilFunctions(awsSecretKey, awsAccessKey, domainName);
        DomainName = domainName;
        BaseDomain = string.Join(".",DomainName.Split(".")[^2..]);
        EscapedBaseDomain = string.Join("-",DomainName.Split(".")[^2..]);
    }
    public async Task<Result<StackStatus>> Create()
    {
       
        var cloudFile =
            await awsUtilFunctions.ExtractTextFromRemoteFile("https://fissaa-cli.s3.amazonaws.com/test/network.yml");
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
                StackName = NetworkStackName,
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
                        Value = BaseDomain
                    }
                }
            });
            await awsUtilFunctions.WaitUntilStackCreatedOrDeleted(NetworkStackName);

        }
        catch (AlreadyExistsException)
        {
        }
        var status = await awsUtilFunctions.GetStackStatus(NetworkStackName);
        return awsUtilFunctions.StackStatusIsSuccessfull(status) ? Result.Success(status): Result.Failure<StackStatus>("creating network failed") ;


    }
    
    public async Task Destroy()
    {
        await awsUtilFunctions.DeleteStack(NetworkStackName);
    }
}