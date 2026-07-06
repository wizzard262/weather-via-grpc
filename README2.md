# Azure Container Apps setup

- The CLIENT: **WeatherApi** needs to go to an Azure Web App.
- The SERVER: **WeatherService - ContainerImage** needs to go to an ACR (Azure Container Registry).
- An ACA (Azure Container App) needs to run the ACR **WeatherService - ContainerImage**

---
### Step 1 — Publish the client
The Client is published directly from Visual Studio diredtly to an Azure Web App Resource:   
The homepage can be seen at: [https://grpc-weather-client-hhfmhrbhemavfte3.uksouth-01.azurewebsites.net/]()  
See system diagram at: [https://grpc-weather-client-hhfmhrbhemavfte3.uksouth-01.azurewebsites.net/images/diagram.png]()

### Step 2 — Create ACR (Azure Container Registry) on Azure
- Azure Portal --> new **ContainerRegistry**
- Registry Name: containerregistryweather
- Region: UK South
- Pricing: Basic
- Registry Domain Name: [containerregistryweather.azurecr.io]()

### Step 3 - Save local Docker image to .tar file
Admin Powershell  
`cd C:\DEV\Repositories\GitHub\weather-via-grpc\WeatherService\`
- `docker images` List all images: "weather-service:latest"
- `docker save -o weather-service.tar weather-service:latest` Export image  
- `dir` See it has created: "weather-service.tar" 

### Step 4 - Upload image to Azure Container Registry via Docker
Admin Powershell  
`cd C:\DEV\Repositories\GitHub\weather-via-grpc\WeatherService\`  

- `docker tag weather-service:latest containerregistryweather.azurecr.io/weather-service:latest`  
Creates a second name for the image.  
- `docker images`  
Lists: **weather-service:latest** and **containerregistryweather.azurecr.io/weather-service:latest** 
- `az acr login --name containerregistryweather`  
Login Succeeded.  
- `docker push containerregistryweather.azurecr.io/weather-service:latest`  
Image pushed to Azure ACR.
- Azure Portal --> ACR "containerregistryweather" --> Services --> Repositories  
  Lists the Repository: **weather-service**
  
  
##### Redeploy Server image after code change
- Open Docker.Desktop to start the Docker Engine.
- Re-publish the C# Code in Visual Studio to local file system, then run in Admin Powershell:  
*(changing the version v2 to: v3, v4, etc)*

```
az acr login --name containerregistryweather
cd C:\DEV\Repositories\GitHub\weather-via-grpc\WeatherService\
docker build -t weather-service .
docker tag weather-service containerregistryweather.azurecr.io/weather-service:v2
docker push containerregistryweather.azurecr.io/weather-service:v2
```
- Azure Portal --> **containerregistryweather** --> Services --> Repositories --> weather-service  --> Tags: latest
  Lists all images including your new one "v2".  
- Azure Portal --> **container-app-weather** --> Application  -->  Containers --> 
  Change the image tag from say: v1 to v2  
  Save as a new Revision *(old revision should have been auto deleted)*

### Step 5 - Create ACA (Azure Container App) on Azure pointing to the ACR image

#### Create ACA
Azure Portal --> search Container Apps  --> Click Create --> Container App  
- Container app name: "container-app-weather"
- Deployment source: Container image
- Region: UK South
- Registry: containerregistryweather.azurecr.io
- Image: weather-service
- Image tag: latest
- CPU and memory  -> choose min: 0.25 CPU cores, 0.5 Gi memory 
	
Container App **container-app-weather** created.  
- Azure Portal --> Container App **container-app-weather**  --> Overview --> Application Url:  
  https://container-app-weather.thankfultree-2d5a04b9.uksouth.azurecontainerapps.io/    
- Azure Portal --> Container App **container-app-weather**  --> Application --> Scale --> Min Replicas:0  
  *Must set scale-to-zero so the Container will go to sleep and cost almost nothing*
  
#### Setup ACA Ingress

Azure Portal --> Container App **container-app-weather**  --> Networking --> Ingress --> Tick Ingress box.
- Ingress traffic: Accepting traffic from anywhere
- Ingress type: HTTP
- Client Cert: Ignore
- Transport: HTTP/2
- Insecure connections: False (accept only HTTPS not HTTP)
- Target Port: 5000
- Additional TCP Port: do not add any
- SAVE
#### Check ACA Running
Azure Portal --> Container App **container-app-weather**  --> Application --> Revisions and replicas --> see Active revisions list.  
- Lists: **container-app-weather--82zassb**
- Click it --> Running status: **Running**  

#### Test ACA in Browser
- https://container-app-weather.thankfultree-2d5a04b9.uksouth.azurecontainerapps.io/  default Server HTML page
- https://container-app-weather.thankfultree-2d5a04b9.uksouth.azurecontainerapps.io/weather?longitude=53.4&latitude=-2.15 calls HTTP endpoint returning weather JSON.

### Step 6 - View ACA Logs
ACA provides built‑in logging:
*Container stdout/stderr, Kestrel startup logs, port binding logs, gRPC request logs, REST request logs*  
These logs look exactly like local Docker logs because ACA runs the same container image.

- Azure Portal --> Container App **container-app-weather**  --> Monitoring--> Log stream  
 Console.log lines show progress so we can see:  
 The request, the HTTP call to open-meteo, the succeful open-meteo response.
 
 ```
 2026-07-04T14:02:11.0781154Z stdout F 1. gRPC WeatherRequest received
2026-07-04T14:02:11.0796387Z stdout F info: System.Net.Http.HttpClient.Default.LogicalHandler[100]
2026-07-04T14:02:11.0796631Z stdout F       Start processing HTTP request GET https://api.open-meteo.com/v1/forecast?*
2026-07-04T14:02:11.0796745Z stdout F info: System.Net.Http.HttpClient.Default.ClientHandler[100]
2026-07-04T14:02:11.0796819Z stdout F       Sending HTTP request GET https://api.open-meteo.com/v1/forecast?*
2026-07-04T14:02:11.0999826Z stdout F info: System.Net.Http.HttpClient.Default.ClientHandler[101]
2026-07-04T14:02:11.1000187Z stdout F       Received HTTP response headers after 21.2954ms - 200
2026-07-04T14:02:11.1000339Z stdout F info: System.Net.Http.HttpClient.Default.LogicalHandler[101]
2026-07-04T14:02:11.1000356Z stdout F       End processing HTTP request after 21.5513ms - 200
2026-07-04T14:02:11.1002522Z stdout F 2. gRPC WeatherRequest result received
2026-07-04T14:02:11.1002612Z stdout F 3. gRPC WeatherRequest result successful
 ```

