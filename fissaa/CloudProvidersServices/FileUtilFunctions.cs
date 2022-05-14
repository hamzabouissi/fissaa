using fissaa.commands.Templates;

namespace fissaa.CloudProvidersServices;

public class FileUtilFunctions
{

    public void DeleteEnvFile()
    {
        File.Delete("env.txt");
    }

    public void CreateEnvFile(string accessKey,string secretKey,string region, string domainName,
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
}