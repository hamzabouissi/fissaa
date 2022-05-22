using fissaa.commands.Templates;

namespace fissaa.CloudProvidersServices;

public class FileUtilFunctions
{

    public static void DeleteEnvFile()
    {
        File.Delete("env.txt");
    }

    public static void CreateGhostEnvFile(string accessKey,string secretKey,string region, string domainName,
        DatabaseAuth dbAuth,string bucketName,MailAuth mailAuth)
    {
        var smtp = mailAuth.MailProvider switch
        {
            "sendinblue" => "smtp-relay.sendinblue.com",
            _ => throw new ArgumentException($"{mailAuth.MailProvider} isn't found")
        };
        File.AppendAllText("env.txt",$"storage__active=ghost-s3\n");
        File.AppendAllText("env.txt",$"storage__ghost-s3__accessKeyId={accessKey}\n");
        File.AppendAllText("env.txt",$"storage__ghost-s3__secretAccessKey={secretKey}\n");
        File.AppendAllText("env.txt",$"storage__ghost-s3__bucket={bucketName}\n");
        File.AppendAllText("env.txt",$"storage__ghost-s3__region={region}\n");
        
        File.AppendAllText("env.txt",$"mail__transport=SMTP\n");
        File.AppendAllText("env.txt",$"mail__options__host={smtp}\n");
        File.AppendAllText("env.txt",$"mail__options__auth__user={mailAuth.MailEmail}\n");
        File.AppendAllText("env.txt",$"mail__options__auth__pass={mailAuth.MailPassword}\n");

        
        File.AppendAllText("env.txt",$"database__client={dbAuth.engine}\n");
        File.AppendAllText("env.txt",$"database__connection__host={dbAuth.dbHost}\n");
        File.AppendAllText("env.txt",$"database__connection__user={dbAuth.username}\n");
        File.AppendAllText("env.txt",$"database__connection__password={dbAuth.password}\n");
        File.AppendAllText("env.txt",$"database__connection__database={dbAuth.dbName}\n");
        File.AppendAllText("env.txt",$"url=https://{domainName}/\n");
        File.AppendAllText("env.txt","NODE_ENV=production\n");

    }

    public static void CreateAwsCreedentailsFile(string secret_key,string access_key)
    {
        var secret = $"secret_key={secret_key}" + Environment.NewLine;
        var access = $"access_key={access_key}"+ Environment.NewLine;
        File.WriteAllText(".aws_creedentials.txt", string.Concat(secret,access));

        // File.AppendAllText(".aws_creedentials.txt",$"secret_key={secret_key}\n");
        // File.AppendAllText(".aws_creedentials.txt",$"access_key={access_key}");
    }
}