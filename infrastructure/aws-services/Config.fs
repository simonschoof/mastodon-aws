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