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
using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using Flurl.Http;
using Task = System.Threading.Tasks.Task;

namespace fissaa.CloudProvidersServices;

public class AwsUtilFunctions
{
    private readonly AmazonECRClient _clientEcr;
    public readonly AmazonCloudFormationClient ClientCformation;
    private readonly AmazonECSClient _clientEcs;
    private readonly AmazonSecurityTokenServiceClient _stsClient;
    private readonly AmazonElasticLoadBalancingV2Client _elasticLoadBalancingV2Client;
    private readonly AmazonS3Client _s3Client;
    private RegionEndpoint Region  => RegionEndpoint.USEast1;


    public AwsUtilFunctions(string awsSecretKey,string awsAccessKey)
    {
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        
        _clientEcs = new AmazonECSClient(credentials:auth,Region);
        _clientEcr = new AmazonECRClient(credentials:auth,Region);
        _stsClient = new AmazonSecurityTokenServiceClient(auth,Region);
        ClientCformation = new AmazonCloudFormationClient(auth,Region);
        _elasticLoadBalancingV2Client = new AmazonElasticLoadBalancingV2Client(auth, Region);
        _s3Client = new AmazonS3Client(auth,Region);
    }

    public bool StackStatusIsSuccessfull(StackStatus? status)
    {
        return status is not null && (status == StackStatus.CREATE_COMPLETE || status == StackStatus.UPDATE_COMPLETE ) ;
    }

    public async Task<StackStatus?> GetStackStatus(string stackName)
    {
        try
        {
            var stacksResponse = await ClientCformation.DescribeStacksAsync(new DescribeStacksRequest
            {
                StackName = stackName
            });
            var stackStatus = stacksResponse.Stacks.First().StackStatus;
            return stackStatus;
        }
        catch (Exception )
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
            try
            {
                var eventsResponse = await ClientCformation.DescribeStackResourcesAsync(
                    new DescribeStackResourcesRequest()
                    {
                        StackName = stackName
                    });
                foreach (var resource in eventsResponse.StackResources)
                    Console.WriteLine($"{resource.ResourceType}, status = {resource.ResourceStatus}");
            }
            catch (Exception )
            {
                break;
            }
            
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
    public async Task WaitUntilStackCreatedOrDeleted(string stackName)
    {
        var endStatus = new List<StackStatus>()
        {
            StackStatus.DELETE_COMPLETE,
            StackStatus.DELETE_FAILED,
            StackStatus.CREATE_COMPLETE,
            StackStatus.CREATE_FAILED,
            StackStatus.UPDATE_COMPLETE,
            StackStatus.UPDATE_FAILED
        };
        
        var stackStatus = await GetStackStatus(stackName);
        while (stackStatus!=null && !endStatus.Exists(x=>x ==stackStatus))
        {
            stackStatus = await GetStackStatus(stackName);
            Thread.Sleep(5000);
        }
    }
    public async Task<DeleteStackResponse?> DeleteStack(string stackName)
    {
        try
        {
            var deleteServiceStackResponse = await ClientCformation.DeleteStackAsync(new DeleteStackRequest
            {
                StackName = stackName
            });
            var stackStatus = await GetStackStatus(stackName);
            while (stackStatus != null && stackStatus != StackStatus.DELETE_COMPLETE)
            {
                Thread.Sleep(5);
                stackStatus = await GetStackStatus(stackName);
            }
            return deleteServiceStackResponse;
        }
        catch (StackNotFoundException)
        {
            return null;
        }
                
    }
    public async Task DeleteService(string clusterName,string serviceName)
    {
        var tasksResponse = await _clientEcs.ListTasksAsync(new ListTasksRequest
        {
            Cluster = clusterName,
        });
        foreach (var task in tasksResponse.TaskArns)
        {
            await _clientEcs.StopTaskAsync(new StopTaskRequest
            {
                Cluster = clusterName,
                Task = task
            });
        }
        try
        {
            var response = await _clientEcs.DeleteServiceAsync(new DeleteServiceRequest
            {
                Cluster = clusterName,
                Force = true,
                Service = serviceName
            });
            Console.WriteLine($"status: {response.HttpStatusCode}");
        }
        catch (ServiceNotFoundException)
        {
            Console.WriteLine("Service Not Found on Ecs...");
        }
    }
    public async Task<string> GetAccountId()
    {
        var getCallerIdentityResponse = await _stsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest());
        var accountId = getCallerIdentityResponse.Arn.Split(":")[4];
        return accountId;
    }

    public string GetRegistry(string accountId,string region="us-east-1")=>$"{accountId}.dkr.ecr.{region}.amazonaws.com";
    public async Task<string> ExtractTextFromRemoteFile(string url)
    {
        var text = await url.GetStringAsync();
        return text;
    }
    
    public async Task<(string imageName, string registry)> BuildImage(string dockerfile,string repoName)
    {
        var accountId = await GetAccountId();
        var dateNow = DateTimeOffset.Parse(DateTime.Now.ToString());
        var tag = dateNow.ToUnixTimeMilliseconds();
        var registry = GetRegistry(accountId);
        var imageName = $"{registry}/{repoName}:{tag}";
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
        var tokenResponse = await _clientEcr.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());
        var decodeToken = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(tokenResponse.AuthorizationData.First().AuthorizationToken))
            .Split(":")[1];
        return decodeToken;
    }

    public static async Task LoginToRegistry(string decodeToken, string registry)
    {
        
        await Cli.Wrap("docker")
            .WithArguments(args => args
                .Add("logout"))
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .ExecuteBufferedAsync();
        await Cli.Wrap("docker")
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
        await Cli.Wrap("docker")
            .WithArguments(args => args
                .Add("push")
                .Add(imageName)
            )
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .ExecuteBufferedAsync();
        // {
        //     switch (cmdEvent)
        //     {
        //         case StandardOutputCommandEvent stdOut:
        //             Console.WriteLine($"Out> {stdOut.Text}");
        //             break;
        //         case StandardErrorCommandEvent stdErr:
        //             Console.WriteLine($"Err> {stdErr.Text}");
        //             break;
        //     }
        // }
        // await Cli.Wrap("docker")
        //     .WithArguments(args => args
        //         .Add("push")
        //         .Add(imageName)
        //     )
        //     .WithValidation(CommandResultValidation.ZeroExitCode)
        //     .WithStandardOutputPipe(PipeTarget.ToStream(output))
        //     .ExecuteBufferedAsync();
     
    }
    public async Task DeleteEcrImages(string repo)
    {
        
        var imagesResponse = await _clientEcr.ListImagesAsync(request: new ListImagesRequest { RepositoryName = repo });
        if (imagesResponse.ImageIds.Count == 0)
            return;
        
        var result = await _clientEcr.BatchDeleteImageAsync(new BatchDeleteImageRequest
        {
            ImageIds = imagesResponse.ImageIds,
            RepositoryName = repo
        });
        result.Failures.ForEach(p=>Console.WriteLine(p.FailureReason));
        await _clientEcr.DeleteRepositoryAsync(new DeleteRepositoryRequest
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
        var rulesResponse = await _elasticLoadBalancingV2Client.DescribeRulesAsync(new DescribeRulesRequest
        {
            ListenerArn = listener.OutputValue

        });
        var max = rulesResponse.Rules.Where(p=>p.Priority!="default").DefaultIfEmpty(new Rule(){Priority = "0"}).Max(p => int.Parse(p.Priority))+1;
        return max;
    }

    public async Task CreateReposityIfNotExist(string reposityName)
    {
       
        try
        {
            await _clientEcr.CreateRepositoryAsync(new CreateRepositoryRequest
            {
                RepositoryName = reposityName,
                ImageTagMutability = ImageTagMutability.IMMUTABLE
            });
        }
        catch (RepositoryAlreadyExistsException )
        {
           
        }
    }

    public async Task UploadS3File(Stream fileStream, string bucketName, string keyName)
    {
        var fileTransferUtility = new TransferUtility(_s3Client);
        await fileTransferUtility.UploadAsync(fileStream, bucketName, keyName);
    }
    
   
}