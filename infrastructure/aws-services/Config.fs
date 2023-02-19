namespace MastodonAwsServices.Config

module Secrets =
    open Amazon.SecretsManager
    open Amazon.SecretsManager.Model
    open System.Threading


    let getSecret (secretName: string) =
        let awsSecretManagerClient = new AmazonSecretsManagerClient()
        let mutable secretValueRequest = GetSecretValueRequest()
        secretValueRequest.SecretId <- secretName


        let asyncSecret =
            async {
                let! result =
                    awsSecretManagerClient.GetSecretValueAsync(secretValueRequest, CancellationToken(false))
                    |> Async.AwaitTask

                return result
            }

        let secretResponse = Async.RunSynchronously(asyncSecret)
        secretResponse.SecretString

module Params =
    open Amazon.SimpleSystemsManagement
    open Amazon.SimpleSystemsManagement.Model
    open System.Threading

    let getParameter (parameterName: string) =
        let client = new AmazonSimpleSystemsManagementClient()
        let mutable parameterRequest = GetParameterRequest()
        parameterRequest.Name <- parameterName

        let asyncParameter =
            async {
                let! result =
                    client.GetParameterAsync(parameterRequest, CancellationToken(false))
                    |> Async.AwaitTask

                return result
            }

        let parameterResponse = Async.RunSynchronously(asyncParameter)
        parameterResponse.Parameter.Value

module Values = 
    open Secrets
    open Params
    open Pulumi
    
    let awsConfig = Config("aws");
    
    // Pulumi
    let resourcePrefix = "mastodon"

    // RDS
    let rdsDbMasterPassword= getSecret"mastodon/rds/db-master-password"

    // Mastodon federatiom
    let localDomain = "social.simonschoof.com"
    let singleUserMode = true
    let defaultLocale="en"

    // Mastodon secrets
    // let secretKeyBase= getSecret "mastodon/secrets/secret-key-base"  //rake secret
    // let otpSecret = getSecret "mastodon/secrets/otp-secret" //rake secret 
    // let vapIdPrivateKey = getSecret "mastodon/secrets/vapid-private-key" //mastodon:webpush:generate_vapid_key
    // let vapIdPublicKey = getSecret "mastodon/secrets/vapid-public-key" //mastodon:webpush:generate_vapid_key 

    // Mastodon deployment
    let railsEnv = "production"
    let railsServeStaticFiles = true
    let railsLogLevel = "warn"
    let nodeEnv = "production"

    // Mastodon postgres
    // let dbHost = getParameter "mastodon/postgres/db-host"
    // let dbUser = getParameter "mastodon/postgres/db-user"
    // let dbName = getParameter "mastodon/postgres/db-name"
    // let dbPass = getSecret "mastodon/postgres/db-pass"
    
    // Mastodon redis
    //let redisHost = getParameter "mastodon/redis/redis-host"

    // Mastodon email
    // let stmpServer = getParameter "mastodon/mail/smtp-server"
    // let smtpPort = getParameter "mastodon/mail/smtp-port"
    // let smtpLogin = getParameter "mastodon/mail/smtp-login"
    // let smtpPassword = getSecret "mastodon/mail/smtp-password"
    // let smtpFromAddress = getParameter "mastodon/mail/smtp-from-address"
    // let smtpDomain= "social.simonschoof.com"
    // SMTP_DELIVERY_METHOD
    // SMTP_AUTH_METHOD
    // SMTP_CA_FILE
    // SMTP_OPENSSL_VERIFY_MODE
    // SMTP_ENABLE_STARTTLS_AUTO
    // SMTP_ENABLE_STARTTLS
    // SMTP_TLS
    // SMTP_SSL

    // Mastodon Amazon S3 and compatible
    // let s3AliasHost = "mastodonmedia.simonschoof.com"
    // let s3Enabled= true
    // let s3Bucket =  getParameter "mastodon/s3/bucket" -> get directly from bucket
    // let awsAccessKeyId = getSecret "mastodon/s3/aws-access-key-id"
    // let awsSecretAccessKey = getSecret "mastodon/s3/aws-access-key-id"
    // let s3Region = awsConfig.Require("region")
    // let s3Protocol = "HTTPS"
    // let s3Hostname = getParameter "mastodon/s3/hostname" -> get directly from bucket

    // Mastodon other
    let skipPostDeploymentMigrations = true

    
