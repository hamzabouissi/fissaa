using Amazon;
using Amazon.ACMPCA;
using Amazon.ACMPCA.Model;
using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Route53;
using Amazon.Route53.Model;
using Amazon.Runtime;
using Task = System.Threading.Tasks.Task;

namespace fissaa;

public class AwsDomainService
{
    public readonly RegionEndpoint Region = RegionEndpoint.USEast1;
    private readonly AmazonRoute53Client route53Client;
    private readonly AmazonCertificateManagerClient acmClient;
    private readonly AmazonCloudFormationClient clientCformation;
    private readonly AwsUtilFunctions awsUtilFunctions;

    public AwsDomainService(string awsSecretKey,string awsAccessKey)
    {
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        route53Client = new AmazonRoute53Client(auth, Region);
        clientCformation = new AmazonCloudFormationClient(auth,Region);
        awsUtilFunctions = new AwsUtilFunctions(awsSecretKey, awsAccessKey,String.Empty);

    }

    public async Task CreateDomain(string domainName)
    {
        Console.WriteLine("Create Hosted Zone...");
        var domainNameExist = await DomainNameExist(domainName);
        if (domainNameExist)
        {
            throw new ArgumentException("domain already Created");
        }
        var response = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest()
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
        var zonesByNameResponse = await route53Client.ListHostedZonesByNameAsync(new ListHostedZonesByNameRequest
        {
            DNSName = domainName,
        });
        return zonesByNameResponse.HostedZones.Count >= 1;
    }
    public async Task<string> GetHostedZoneId(string domainName)
    {
        var listHostedZonesByNameResponse=  await route53Client.ListHostedZonesByNameAsync(new ListHostedZonesByNameRequest
        {
            DNSName = domainName,
        });
        var domain = listHostedZonesByNameResponse.HostedZones.FirstOrDefault();
        if (domain is null)
            throw new ArgumentException($"did you create hosted zone for: {domainName}");
        return domain.Id.Split("/")[^1];
    }

    public async Task<StackStatus?> AddHttps(string domainName)
    {
        Console.WriteLine("Creating Https Certificate Started.....");
        var cloudFile = await awsUtilFunctions.ExtractTextFromRemoteFile("https://fissaa-cli.s3.amazonaws.com/test/domain_certificate.yml");
        var hostedZoneId = await GetHostedZoneId(domainName);
        var domain_for_stack = domainName.Replace(".", "-");   
        var stackName = $"{domain_for_stack}-certificate-stack";
        await clientCformation.CreateStackAsync(new CreateStackRequest
        {
            OnFailure = OnFailure.DELETE,
            Parameters = new List<Parameter>
            {
                new()
                {
                    ParameterKey = "DomainName",
                    ParameterValue = domainName,
                },
                new()
                {
                    ParameterKey = "DnsHostedZoneId",
                    ParameterValue = hostedZoneId,
                }
            },
            StackName = stackName,
            TemplateBody = cloudFile,
            TimeoutInMinutes = 30
        });
        await awsUtilFunctions.DisplayResourcesStatus(stackName);
        return await awsUtilFunctions.GetStackStatus(stackName);
    }
}