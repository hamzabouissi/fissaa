using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

using Amazon.ECR;
using Amazon.ECR.Model;
using Amazon.ECS;
using Amazon.ECS.Model;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using CliWrap;
using CliWrap.Buffered;
using Flurl.Http;
using Task = System.Threading.Tasks.Task;

namespace fissaa;

public class AwsUtilFunctions
{
    private readonly AmazonECRClient ClientEcr;
    public readonly AmazonCloudFormationClient ClientCformation;
    private readonly AmazonECSClient ClientEcs;
    public readonly AmazonSecurityTokenServiceClient StsClient;
    public readonly AmazonElasticLoadBalancingV2Client ElasticLoadBalancingV2Client;

    public RegionEndpoint Region { get; set; } = RegionEndpoint.USEast1;


    public AwsUtilFunctions(string awsSecretKey,string awsAccessKey, string projectName)
    {
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        
        ClientEcs = new AmazonECSClient(credentials:auth,Region);
        ClientEcr = new AmazonECRClient(credentials:auth,Region);
        StsClient = new AmazonSecurityTokenServiceClient(auth,Region);
        ClientCformation = new AmazonCloudFormationClient(auth,Region);
        ElasticLoadBalancingV2Client = new AmazonElasticLoadBalancingV2Client(auth, Region);
    }


    public async Task<StackStatus?> GetStackStatus(string stackName)
    {
        try
        {
            var stacksResponse =await ClientCformation.DescribeStacksAsync(new DescribeStacksRequest
            {
                StackName = stackName
            });
            var stackStatus = stacksResponse.Stacks.First().StackStatus;
            return stackStatus;
        }
        catch (Exception e)
        {
            return null;
        }
       
    }
    public async Task DisplayResourcesStatus(string stackName)
    {
        var endStatus = new List<StackStatus>()
        {
            StackStatus.ROLLBACK_IN_PROGRESS,
            StackStatus.CREATE_IN_PROGRESS,
            StackStatus.DELETE_IN_PROGRESS,
            StackStatus.UPDATE_IN_PROGRESS,
        };
        var stackStatus = await GetStackStatus(stackName);
        if (stackStatus is null)
            return;
        
        while(endStatus.Exists(e=>e==stackStatus))
        {
            var eventsResponse = await ClientCformation.DescribeStackResourcesAsync(new DescribeStackResourcesRequest()
            {
                StackName = stackName
            });
            foreach (var resource in eventsResponse.StackResources)
                Console.WriteLine($"{resource.ResourceType}, status = {resource.ResourceStatus}");

            Thread.Sleep(5);
            stackStatus = await GetStackStatus(stackName);
            if (stackStatus is null)
                Console.WriteLine("Stack Status: DELETE_COMPLETE");
            else
            {
                Console.WriteLine("====>");
                Console.WriteLine($"Stack Status: {stackStatus}");
                Console.WriteLine("====>");
            }
            
        }
    }
    public async Task<DeleteStackResponse?> DeleteStack(string stackName)
    {
        try
        {
            Console.WriteLine($"Delete Stack {stackName}");
            var deleteServiceStackResponse = await ClientCformation.DeleteStackAsync(new DeleteStackRequest
            {
                StackName = stackName
            });
            var stackStatus = await GetStackStatus(stackName);
            Console.WriteLine("Deleting Stack On Progress");
            while (stackStatus != null && stackStatus != StackStatus.DELETE_COMPLETE)
            {
                Thread.Sleep(5);
                stackStatus = await GetStackStatus(stackName);
            }
            Console.WriteLine("Deleting Stack Completed");
            return deleteServiceStackResponse;
        }
        catch (StackNotFoundException)
        {
            Console.WriteLine($"No Stack {stackName}....");
            return null;
        }
                
    }
    public async Task DeleteService(string ClusterName,string ServiceName)
    {
        var tasksResponse = await ClientEcs.ListTasksAsync(new ListTasksRequest
        {
            Cluster = ClusterName,
        });
        foreach (var task in tasksResponse.TaskArns)
        {
            await ClientEcs.StopTaskAsync(new StopTaskRequest
            {
                Cluster = ClusterName,
                Task = task
            });
        }
        try
        {
            var response = await ClientEcs.DeleteServiceAsync(new DeleteServiceRequest
            {
                Cluster = ClusterName,
                Force = true,
                Service = ServiceName
            });
            Console.WriteLine($"status: {response.HttpStatusCode}");
        }
        catch (ServiceNotFoundException e)
        {
            Console.WriteLine("Service Not Found on Ecs...");
        }
    }
    public async Task<string> GetAccountId()
    {
        var getCallerIdentityResponse = await StsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest());
        var accountId = getCallerIdentityResponse.Arn.Split(":")[4];
        return accountId;
    }

    public string GetRegistry(string accountId,string region="us-east-1")=>$"{accountId}.dkr.ecr.{region}.amazonaws.com";
    public async Task<string> ExtractTextFromRemoteFile(string url)
    {
        var text = await url.GetStringAsync();
        return text;
    }
    
    public async Task<(string imageName, string registry)> BuildImage(string dockerfile,string RepoName)
    {
        Console.WriteLine("BuildImage started");
        var accountId = await GetAccountId();
        var tag = Guid.NewGuid().ToString();
        var registry = GetRegistry(accountId);
        var imageName = $"{registry}/{RepoName}:{tag}";
        var result = await Cli.Wrap("docker")
            .WithArguments(args => args
                .Add("build")
                .Add("-t")
                .Add(imageName)
                .Add(dockerfile)
            )
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();
        var stdErr = result.StandardError;
        if (stdErr.Length>0)
            throw new SystemException(stdErr);
        return (imageName,registry);
    }
    public async Task<string> DecodeRegistryLoginTokenToPassword()
    {
        var tokenResponse = await ClientEcr.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());
        var decodeToken = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(tokenResponse.AuthorizationData.First().AuthorizationToken))
            .Split(":")[1];
        return decodeToken;
    }

    public static async Task LoginToRegistry(string decodeToken, string registry)
    {
        Console.WriteLine("RegistryLogin started");
        var result = await Cli.Wrap("docker")
            .WithArguments(args => args
                .Add("login")
                .Add("--username")
                .Add("AWS")
                .Add("--password")
                .Add(decodeToken)
                .Add(registry)
            )
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .ExecuteBufferedAsync();
    }

    public async Task DeployImageToEcr(string imageName)
    {
        Console.WriteLine("DeployImageToEcr started...");
        var result = await Cli.Wrap("docker")
            .WithArguments(args => args
                .Add("push")
                .Add(imageName)
            )
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .ExecuteBufferedAsync();
        var stdOut = result.StandardOutput;
        var stdErr = result.StandardError;
        // if (stdErr.Length>0)
        // {
        //     throw new SystemException(stdErr);
        // }
        Console.WriteLine(stdOut);
    }
    public async Task DeleteEcrImages(string repo)
    {
        
        var imagesResponse = await ClientEcr.ListImagesAsync(request: new ListImagesRequest { RepositoryName = repo });
        if (imagesResponse.ImageIds.Count == 0)
            return;
        
        Console.WriteLine("DeleteEcrImages started....");
        var result = await ClientEcr.BatchDeleteImageAsync(new BatchDeleteImageRequest
        {
            ImageIds = imagesResponse.ImageIds,
            RepositoryName = repo
        });
        result.Failures.ForEach(p=>Console.WriteLine(p.FailureReason));
        await ClientEcr.DeleteRepositoryAsync(new DeleteRepositoryRequest
        {
            Force = true,
            RepositoryName = repo
        });

    }
    
    public async Task CreateDockerfile(string projectType)
    {
        var url = "https://gist.githubusercontent.com/hamzabouissi/abc0a0bffe61df2c51cc03ed47a6f1ab/raw/a22072086ca54387216eab3c315b05bab9deeeb6";
        switch (projectType)
        {
            case "Fastapi":
                url = $"{url}/fastapi-dockerfile";
                break;
            case "NodeJs":
                url = $"{url}/nodejs-dockerfile";
                break;
            case "AspNetCore":
                break;
        }
        var dockerfileContent = await url.GetStringAsync();
        await File.WriteAllTextAsync("Dockerfile",dockerfileContent);
    }

    public async Task<int> GetListenerRuleNextPriorityNumber(string stackName)
    {
        var stackDescribeResult = await ClientCformation.DescribeStacksAsync(new DescribeStacksRequest
        {
            StackName = stackName
        });
        var stack = stackDescribeResult.Stacks.First();
        var listener = stack.Outputs.Single(o => o.OutputKey == "HttpsPublicListener");
        var rulesResponse = await ElasticLoadBalancingV2Client.DescribeRulesAsync(new DescribeRulesRequest
        {
            ListenerArn = listener.OutputValue

        });
        var max = rulesResponse.Rules.Where(p=>p.Priority!="default").DefaultIfEmpty(new Rule(){Priority = "0"}).Max(p => int.Parse(p.Priority))+1;
        return max;
    }

    public async Task CreateReposityIfNotExist(string reposityName)
    {
        Console.WriteLine("Create Repository");
        try
        {
            await ClientEcr.CreateRepositoryAsync(new CreateRepositoryRequest
            {
                RepositoryName = reposityName,
            });
        }
        catch (RepositoryAlreadyExistsException )
        {
           
        }
    }
}