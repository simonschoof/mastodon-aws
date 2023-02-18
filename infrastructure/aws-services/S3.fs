namespace MastodonAwsServices


module S3 =

    open Pulumi
    open Pulumi.Aws.Iam
    open Pulumi.Aws.Iam.Inputs
    open Pulumi.Aws.S3
    open Pulumi.FSharp

    let createBucket () = // Create an AWS resource (S3 Bucket)

        let bucket =
            let bucketName = "mastodon-s3-storage"

            let bucketArgs = BucketArgs(Acl = "private")

            Bucket(bucketName, bucketArgs)

        let bucketPublicAccessBlockArgs =
            BucketPublicAccessBlockArgs(
                Bucket = bucket.Id,
                BlockPublicAcls = true,
                BlockPublicPolicy = true,
                IgnorePublicAcls = true,
                RestrictPublicBuckets = true
            )

        BucketPublicAccessBlock("mastodon-s3-storage-public-access-block", bucketPublicAccessBlockArgs)

        //
        // Needed S3 permissions for Mastodon to be able to access th S3 bucket
        // https://gist.github.com/ftpmorph/299c00907c827fbca883eeb45e6a7dc4?permalink_comment_id=4374053
        //
        let limitedPermissionsToOneBucketStatement =
            GetPolicyDocumentStatementInputArgs(
                Effect = "Allow",
                Actions =
                    inputList [ input "s3:ListBucket"
                                input "s3:GetBucketLocation" ],
                Resources = inputList [ io bucket.Arn ]
            )

        let permissionsToBucketStatement =
            GetPolicyDocumentStatementInputArgs(
                Effect = "Allow",
                Actions =
                    inputList [ input "s3:GetObject"
                                input "s3:GetObjectAcl"
                                input "s3:PutObject"
                                input "s3:PutObjectAcl"
                                input "s3:DeleteObject"
                                input "s3:AbortMultipartUpload"
                                input "s3:ListMultipartUploadParts" ],
                Resources = inputList [ io (Output.Format($"{bucket.Arn}/*")) ]
            )

        let policyDocumentInvokeArgs =
            GetPolicyDocumentInvokeArgs(
                Statements =
                    inputList [ input limitedPermissionsToOneBucketStatement
                                input permissionsToBucketStatement ]
            )

        let policyDocument =
            GetPolicyDocument.Invoke(policyDocumentInvokeArgs)
        
        let policyArgs = PolicyArgs(PolicyDocument = io (policyDocument.Apply(fun (pd) -> pd.Json)))

        let policy = Policy("mastodon-s3-access-policiy", policyArgs)

        let  group = Group("mastodon-s3-access-group")

        let policyAttachmentArgs = PolicyAttachmentArgs(Groups = group.Name, PolicyArn = policy.Arn)
        
        PolicyAttachment("mastodon-access-group-policiy-attachment", policyAttachmentArgs)


        // Export the name of the bucket
        [ ("bucketName", bucket.Id :> obj) ]
