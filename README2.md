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

### Step 3 — Create ACA (Azure Container App) on Azure

### Step 4 — Push your image to Azure Container Registry (ACR)

Tag your local image:
`docker tag weather-service:latest <youracr>.azurecr.io/weather-service:latest`
Push it to ?
`docker push <youracr>.azurecr.io/weather-service:latest`
This publishes your container image so Azure Container Apps can pull it.

### Step 5 — Create an Azure Container App (ACA) using your ACR image
Configure the Container App to use your ACR image:  
 - Registry: your ACR  
 - ContainerImage:`<youracr>.azurecr.io/weather-service:latest`

ACA will:  

	- pull your image
	- run it inside a Linux container
	- expose port 5000 via HTTPS
	- optionally expose REST on 7010 internally
	- handle scaling, revisions, logs, metrics

### Step 6 — Configure Ingress
Set ingress to match your gRPC port:

	 - Ingress port: 5000
	 - Transport: HTTP/2
	 - Ingress type: External (public)

ACA maps:

`https://<your-app-name>.<region>.azurecontainerapps.io → container port 5000`

Your gRPC client connects to the ACA URL without specifying a port.

### Step 7 — Set environment variables in ACA
Production configuration goes in ACA, not in your Dockerfile.

Examples:
```
ASPNETCORE_ENVIRONMENT = Production
OPEN_METEO_URL = https://api.open-meteo.com/v1/forecast
```
ACA injects these at runtime.

### Step 8 — View Logs
ACA provides built‑in logging:

- Container stdout/stderr
- Kestrel startup logs
- port binding logs
- gRPC request logs
- REST request logs

These logs look exactly like your local Docker logs because ACA runs the same container image.

---
---

**TODO: When the ACA and ARC setup update:**
-Add to draw.io info on ACA & ACR (save PNG to repo)
-The 2 index.html files
