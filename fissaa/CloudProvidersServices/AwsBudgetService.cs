using System.Net;
using Amazon;
using Amazon.Budgets;
using Amazon.Budgets.Model;
using Amazon.CostExplorer;
using Amazon.CostExplorer.Model;
using Amazon.Runtime;
using CSharpFunctionalExtensions;
using fissaa.CloudProvidersServices;
using Subscriber = Amazon.Budgets.Model.Subscriber;

namespace fissaa;

public class AwsBudgetService
{
    public readonly RegionEndpoint Region = RegionEndpoint.USEast1;
    private readonly AmazonBudgetsClient _budgetsClient;
    private readonly AwsUtilFunctions _awsUtilFunctions;
    private readonly AmazonCostExplorerClient _costExplorerClient;

    public AwsBudgetService(string awsSecretKey,string awsAccessKey)
    {
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        _budgetsClient = new AmazonBudgetsClient(auth, Region);
        _costExplorerClient = new AmazonCostExplorerClient(auth, Region);
        _awsUtilFunctions = new AwsUtilFunctions(awsSecretKey, awsAccessKey);

    }

    public async Task<Result> Create(string domainName,string email,decimal budget,decimal limit)
    {
        var acountId = await _awsUtilFunctions.GetAccountId();
        var baseDomain = domainName;
        var tag = baseDomain;
        var createBudgetResponse = await _budgetsClient.CreateBudgetAsync(new CreateBudgetRequest
        {
            AccountId = acountId,
            Budget = new Budget
            {
                BudgetLimit = new Spend
                {
                    Amount = budget,
                    Unit = "USD"
                },
                BudgetName = $"{baseDomain}-cost",
                BudgetType = BudgetType.COST,
                CostFilters = new Dictionary<string, List<string>>()
                {
                    {
                        "TagKeyValue", new List<string>()
                        {
                            $"user:app-domain${tag}"
                        }
                    }
                },
                TimeUnit = TimeUnit.MONTHLY
            },
            NotificationsWithSubscribers = new List<NotificationWithSubscribers>()
            {
                new()
                {
                    Notification = new Notification
                    {
                        ComparisonOperator = ComparisonOperator.GREATER_THAN,
                        NotificationState = null,
                        NotificationType = NotificationType.ACTUAL,
                        Threshold = (double)limit,
                        ThresholdType = ThresholdType.ABSOLUTE_VALUE
                    },
                    Subscribers = new List<Subscriber>
                    {
                        new()
                        {
                            Address = email,
                            SubscriptionType = SubscriptionType.EMAIL
                        }
                    }
                }
            },

        });
        return createBudgetResponse.HttpStatusCode==HttpStatusCode.OK ? Result.Success():Result.Failure("Budget Failed") ;
    }

    public async Task<Result> Delete(string domainName)
    {
        var acountId = await _awsUtilFunctions.GetAccountId();
        await _budgetsClient.DeleteBudgetAsync(new DeleteBudgetRequest
        {
            AccountId = acountId,
            BudgetName = $"{domainName}-cost",
        });
        return Result.Success();
    }

    public async Task<Dictionary<string, float>> ListCost(string domainName)
    {
        var startDate = (DateTime.Today.ToString("yyyy-MM")+"-01");
        var endDate = DateTime.Today.ToString("yyyy-MM-dd");

        // var baseDomain = string.Join(".",domainName.Split(".")[^2..]);
        var getCostAndUsageResponse = await _costExplorerClient.GetCostAndUsageAsync(new GetCostAndUsageRequest
        {
            Granularity = Granularity.DAILY,
            GroupBy = new List<GroupDefinition>()
            {
                new ()
                {
                    Key = "app-domain",
                    Type = GroupDefinitionType.TAG
                }
            },
            Metrics = new List<string>()
            {
                "UnblendedCost"
            },
            TimePeriod = new DateInterval
            {
                End = endDate,
                Start = startDate
            }
        });
        var pricePerDay = new Dictionary<string, float>();
        foreach (var costResultByTime in getCostAndUsageResponse.ResultsByTime)
        {
            var date = costResultByTime.TimePeriod.Start;
            var group = costResultByTime.Groups.SingleOrDefault(g => g.Keys.Contains($"app-domain${domainName}"));
            if (group is null)
                continue;
            var amount = float.Parse(group.Metrics.First().Value.Amount);
            pricePerDay.Add(date, amount);
        }

        return pricePerDay;
    }
}