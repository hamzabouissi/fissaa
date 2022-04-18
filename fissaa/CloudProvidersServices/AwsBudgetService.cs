using System.Net;
using Amazon;
using Amazon.Budgets;
using Amazon.Budgets.Model;
using Amazon.Runtime;
using CSharpFunctionalExtensions;

namespace fissaa;

public class AwsBudgetService
{
    public readonly RegionEndpoint Region = RegionEndpoint.USEast1;
    private readonly AmazonBudgetsClient budgetsClient;
    private readonly AwsUtilFunctions awsUtilFunctions;

    public AwsBudgetService(string awsSecretKey,string awsAccessKey)
    {
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        budgetsClient = new AmazonBudgetsClient(auth, Region);
        awsUtilFunctions = new AwsUtilFunctions(awsSecretKey, awsAccessKey, string.Empty);

    }

    public async Task<Result> Create(string domainName,string email,decimal budget,decimal limit)
    {
        var acountId = await awsUtilFunctions.GetAccountId();
        var baseDomain = domainName;
        var tag = baseDomain;
        //todo activate cost allocation tag
        var createBudgetResponse = await budgetsClient.CreateBudgetAsync(new CreateBudgetRequest
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
        var acountId = await awsUtilFunctions.GetAccountId();
        await budgetsClient.DeleteBudgetAsync(new DeleteBudgetRequest
        {
            AccountId = acountId,
            BudgetName = domainName
        });
        return Result.Success();
    }
}