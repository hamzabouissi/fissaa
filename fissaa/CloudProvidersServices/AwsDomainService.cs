using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Route53;
using Amazon.Route53.Model;
using Amazon.Runtime;
using CSharpFunctionalExtensions;
using Tag = Amazon.CloudFormation.Model.Tag;
using Task = System.Threading.Tasks.Task;

namespace fissaa.CloudProvidersServices;

public class AwsDomainService
{
    public readonly RegionEndpoint Region = RegionEndpoint.USEast1;
    private readonly AmazonRoute53Client _route53Client;
    private readonly AmazonCloudFormationClient _clientCformation;
    private readonly AwsUtilFunctions _awsUtilFunctions;

    public AwsDomainService(string awsSecretKey,string awsAccessKey)
    {
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        _route53Client = new AmazonRoute53Client(auth, Region);
        _clientCformation = new AmazonCloudFormationClient(auth,Region);
        _awsUtilFunctions = new AwsUtilFunctions(awsSecretKey, awsAccessKey);

    }

    public async Task CreateDomain(string domainName)
    {
        Console.WriteLine("Create Hosted Zone...");
        var domainNameExist = await DomainNameExist(domainName);
        if (domainNameExist)
        {
            throw new ArgumentException("domain already Created");
        }

        var response = await _route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest()
        {
            Name = domainName,
            CallerReference = Guid.NewGuid().ToString()
           
        });
        Console.WriteLine("NameServers: ");
        response.DelegationSet.NameServers.ForEach(Console.WriteLine);
        Console.WriteLine("You need to add those nameservers to your dns provider");
        Console.WriteLine("Wait Until dns propagation work, means your dns provider recognize those nameservers, it may take 24h Max");
    }

    private async Task<bool> DomainNameExist(string domainName)
    {
        var zonesByNameResponse = await _route53Client.ListHostedZonesByNameAsync(new ListHostedZonesByNameRequest
        {
            DNSName = domainName,
            MaxItems = "1"
        });
        return zonesByNameResponse.HostedZones.First().Name[..^1] == domainName;
    }
    public async Task<string> GetHostedZoneId(string domainName)
    {
        var listHostedZonesByNameResponse=  await _route53Client.ListHostedZonesByNameAsync(new ListHostedZonesByNameRequest
        {
            DNSName = domainName,
        });
        var domain = listHostedZonesByNameResponse.HostedZones.FirstOrDefault();
        if (domain is null)
            throw new ArgumentException($"did you create hosted zone for: {domainName}");
        return domain.Id.Split("/")[^1];
    }

    public async Task<Result> AddHttps(string domainName)
    {
        
        var cloudFile = await _awsUtilFunctions.ExtractTextFromRemoteFile("https://fissaa-cli.s3.amazonaws.com/test/domain_certificate.yml");
        var baseDomain = string.Join(".",domainName.Split(".")[^2..]); 
        
        var hostedZoneId = await GetHostedZoneId(baseDomain);
        var domainForStack = baseDomain.Replace(".", "-");   
        var stackName = $"{domainForStack}-certificate-stack";
        
        var status = await _awsUtilFunctions.GetStackStatus(stackName);
        if (_awsUtilFunctions.StackStatusIsSuccessfull(status))
            return Result.Success(status);
        
        await _clientCformation.CreateStackAsync(new CreateStackRequest
        {
            OnFailure = OnFailure.DELETE,
            Parameters = new List<Parameter>
            {
                new()
                {
                    ParameterKey = "DomainName",
                    ParameterValue = baseDomain,
                },
                new()
                {
                    ParameterKey = "DnsHostedZoneId",
                    ParameterValue = hostedZoneId,
                }
            },
            StackName = stackName,
            TemplateBody = cloudFile,
            TimeoutInMinutes = 30,
            Tags = new List<Tag>()
            {
                new Tag
                {
                    Key = "app-domain",
                    Value = baseDomain
                }
            } 
        });
        await _awsUtilFunctions.WaitUntilStackCreatedOrDeleted(stackName);
        status = await _awsUtilFunctions.GetStackStatus(stackName);
        return _awsUtilFunctions.StackStatusIsSuccessfull(status) ? Result.Success(status): Result.Failure("creating https failed") ;
    }
}