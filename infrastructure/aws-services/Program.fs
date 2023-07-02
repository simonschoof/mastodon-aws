module Program

open MastodonAwsServices.S3AndCloudFront
open Pulumi.FSharp

let infra () =

  createBucketAndDistribution () 

  dict []

[<EntryPoint>]
let main _ =
  Deployment.run infra
