# 🔐 Secure Vault

SecureVault is a storage app that uses End-to-End encryption. You can store your files safely without worrying about who has access to your data.

<img src="./docs/preview.png" width="75%">

## 🌐 Technologies

-   C# ASP NET
-   React (Vite)
-   Amazon S3
-   PostgreSQL
-   Docker & Docker Compose

## ⚪ How it works / Architecture

<img src="./docs/architecture.png" width="60%">

-   To make uploads, the client sends a request to the backend API which will return with one or more Presigned-Urls
-   The client encrypts the file before uploading
-   It also chunks the file if it's too big (>100mb) and then upload each chunk using the URL

### 🔑 Encryption flow

<img src="./docs/encryption.png" width="50%">

-   The **ROOT_KEY** is generated on account creation, it's a random 32 byte key
-   The user's is asked to provide a secret, this **SECRET_KEY** it's used to encrypt the **ROOT_KEY**.
-   When a folder is created, it's generated a **FOLDER_KEY** (32 byte), this **FOLDER_KEY** is encrypted by the **ROOT_KEY**
-   When a file is created, it's generated a **FILE_KEY** (32 byte), if the file has a parent folder, the **FOLDER_KEY** is responsible for encrypting this **FILE_KEY**.
-   The **FILE_KEY** is used to encrypt the file blob & content.
-   **This way, only the user with his secret can decrypt all the keys.**

&nbsp;

# 🔨 Deployment

**You must have a AWS S3 bucket created or a software that has S3 compatible API's installed in your server (Garage, Minio)**

-   Garage setup: [Quick start](https://garagehq.deuxfleurs.fr/documentation/quick-start/)
-   Garage web ui: [Docker compose](https://github.com/khairul169/garage-webui)
-   AWS: [Getting started with Amazon s3](https://docs.aws.amazon.com/AmazonS3/latest/userguide/GetStartedWithS3.html)

**You must have AWS CLI installed somewhere, in your server or locally.**

-   [AWS CLI installation](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html)
-   [AWS CLI Configuration](https://docs.aws.amazon.com/cli/v1/userguide/cli-configure-files.html)

This is how i configured AWS Credentials for garage:

```bash
# vim ~/.aws/config
[profile garage]
region = us-east-1
output = json
endpoint_url = http://your_garage_endpoint.com

# vim ~/.aws/credentials
[garage]
aws_access_key_id = garage_access_key
aws_secret_access_key = garage_secret_key
```

### ⚠️ Configure S3 CORS (Important)

The client-side code makes several requests directly to S3 via presigned-urls, therefore we need to create some CORS configuration using the AWS CLI:

```bash
aws --endpoint <endpoint> s3api put-bucket-cors \
  --bucket <bucket-name> \
  --cors-configuration '{
    "CORSRules": [{
      "AllowedHeaders": ["*"],
      "AllowedMethods": ["GET", "PUT", "POST", "HEAD"],
      "AllowedOrigins": ["*"],
      "ExposeHeaders": ["ETag"]
    }]
  }' \
  --profile garage
```

### Pull the repository

```bash
   git pull git@github.com:jpedro-cf/SecureVault.git
```

### RSA Keys Creation (For JWT)

-   Create your RSA Keys and insert them in **.env** as base64 encoded.

```bash
# already in .gitignore
mkdir -p secrets
cd secrets

openssl genrsa > private.pem
openssl rsa -in private.pem -pubout -out public.pem

# Print the base64 encoded keys so you can copy them.
printf '%s' $($(echo cat ./public.pem) | base64) # public
printf '%s' $($(echo cat ./private.pem) | base64) # private
```

### Start the App

-   In your server, use the environment variables following **.env.example**
-   Start docker compose

```bash
   docker compose up -d
```

-   You may need to change the docker network if you're not deploying in [Dokploy](https://docs.dokploy.com/docs/core)
-   Your domains also need to be configured.

&nbsp;

# 🔨 Development

The project dependencies are on **dev-docker-compose.yml**. It contains:

-   Localstack
-   PostgreSQL
-   PgAdmin

You'll also need to have .NET tools installed:

-   [.NET installation on linux](https://learn.microsoft.com/en-us/dotnet/core/install/linux)
-   [Install dotnet-ef tool](https://learn.microsoft.com/en-us/ef/core/cli/dotnet#installing-the-tools)

&nbsp;

Export the environment variables for the current shell session (will be needed for dotnet)

```bash
set -a
source .env
set +a
```

Run development containers

```bash
docker compose -f dev-docker-compose.yml up -d
```

Start the dotnet api & apply migrations

```bash
cd server

dotnet restore
dotnet ef database update
dotnet run
```

Start the client react application

-   [pnpm installation](https://pnpm.io/installation)
-   Make sure the **VITE_CLIENT_URL** & **VITE_SERVER_URL** are correct in ./client/.env

```bash
cd client

pnpm install
pnpm run dev
```
