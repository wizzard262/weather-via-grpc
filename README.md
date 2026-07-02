
# Local Docker Setup & Run Guide for .NET10 Projects

## 1.  Use Weather Client to connect to WeatherService in Visual Studio 
- Open in Visual Studio: `C:\DEV\Repositories\GitHub\weather-via-grpc\weather-via-grpc.slnx`
- Configure StartUp Projects --> Start both **WeatherApi** and **WeatherService**
- Click PLAY button
- See the CLIENT: **WeatherApi** open a browser: [https://localhost:7034]() showing HTML page.
- See the SERVER: **WeatherService** open a browser: [https://localhost:7010]() showing HTML page.  
  *N.B. The SERVER is also open on [http://localhost:5000]() for **HTTP** and **gRPC** requests (but not with https).*    
  *Note: Minimal API endpoints allow HTTP2 and so work on port 5000 , the Controller endpoint /weather does not* 
  
### Which WeatherService SERVER paths work in browser?
 
- [http://localhost:5000]()                                       - info HTML page
- [http://localhost:5000/ping]()                                  - server alive test
- [http://localhost:5000/weather?longitude=53.4&latitude=-2.15]() - ERROR! "An HTTP/1.x request was sent to an HTTP/2 only endpoint."
	
---
	
- [https://localhost:5000](){all paths}                           - ERR_SSL_PROTOCOL_ERROR

---
	
- [http://localhost:7010]()                                       - info HTML page			
- [http://localhost:7010/ping]()                                  - ERR_EMPTY_RESPONSE
- [http://localhost:7010/weather?longitude=53.4&latitude=-2.15]() - ERR_EMPTY_RESPONSE
	
---	

- [https://localhost:7010]()                                       - info HTML page
- [https://localhost:7010/ping]()                                  - server alive test
- [https://localhost:7010/weather?longitude=53.4&latitude=-2.15]() - REST weather JSON
### Accessing the gRPC endpoint
 It cannnot be done over HTTP in the browser.  
- The WeatherApi CLIENT: must send the gRPC request  
- In WeatherApi HTML homepage [https://localhost:7034]() click GetWeather button.
- The form sends HTTP GET to :[https://localhost:7034/weather?latitude=53.41&longitude=-2.15]() via javascript.
- The client then calls the server on [https://localhost:5000]() and returns the result.
- Javascript processes the result and updates the HTML homepage with the results:

```
{
  "temperature_2m": 16.8,
  "weather_code": 3,
  "weather_description": "Overcast",
  "raw_protobuf": "09 CD CC CC CC CC CC 30 40 10 03 1A 08 4F 76 65 72 63 61 73 74"
}
``` 

## 2. Install Docker Software
You must have these installed before building or running containers:

### Docker Desktop
- Download: [https://www.docker.com/products/docker-desktop/]()  
  *(Windows - AMD64, Docker Desktop Installer.exe)*
- Run 'Docker Desktop Installer.exe' *(install to folder: `C:\Program Files\Docker\Docker\*`)*  
      * Check  `C:\Windows\System32\wsl.exe` is present.   
	  * Admin PowerShell: *(Must be 64 bit)* i.e. at: `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe`  
       * Run: `wsl --install`  
	    Downloads & Installs WSL (Windows Subsystem for Linux 2.7.10) & Ubuntu.
	    Created a default Unix user: **account:jones pw:password**  
	    *(To run Ubuntu distro from admin PowerShell: `wsl -d Ubuntu`)*   
- Check Docker Engine is running:  
      * Any prompt any folder: `docker --version` *(prints: Docker version 29.6.1)*  
	  * Open Docker.Desktop App --> green bottom left message 'Engine running'

### .NET 10 SDK (optional for local builds)
- Download: [https://dotnet.microsoft.com/en-us/download/dotnet/10.0]()


## 3. Dockerfile
- There is a **Dockerfile** already in the root of gRPC server project:
   ```C:\DEV\Repositories\GitHub\weather-via-grpc\WeatherService\Dockerfile```  
   *(always name it "Dockerfile", no extension)*   
	It uses the 'official .NET 10 LTS runtime Microsoft container images' as the base:  
	*(i.e. mcr.microsoft.com/dotnet/aspnet:10.0 & mcr.microsoft.com/dotnet/sdk:10.0)*   
	It exposes port 5000.

## 4. Publish the Service  

   In Visual Studio, publish the **WeatherService** project to:  
  `C:\DEV\Repositories\GitHub\weather-via-grpc\WeatherService\bin\Release\net10.0\publish`  
  *This output becomes the content of the **ContainerImage**.* 

## 5. Build a Docker Container Image and run it
The **DockerEngine** runs **Containers** which run **ContainerImages**.

 
### Admin PowerShell:  
  `cd C:\DEV\Repositories\GitHub\weather-via-grpc\WeatherService\`  
- **Containers - DELETE**  
   `docker ps -aq | ForEach-Object { docker rm -f $_ }` Delete all existing  (running or stopped)  
   `docker ps -a`  List all.  
   **OR**  
   Docker.Desktop: Containers --> Delete All.
   
 - **ContainerImages - DELETE**  
  `docker images -q | ForEach-Object { docker rmi -f $_ }` Delete all existing  (optional)  
  `docker images` List all.  
  **OR**  
  Docker.Desktop: Images --> Delete All.
	
- **ContainerImage - BUILD**  
    Docker builds a **ContainerImage** and stores it locally inside Docker Desktop’s internal image store, not as a file on disk.  
	`docker build -t weather-service:latest .` Build new **ContainerImage**      
    `docker images` List all.   
   **OR**  
   Docker.Desktop --> **Cannot be done**  
   
   
-  **DockerEngine - BUILD and RUN Container**  - using the new **ContainerImage**  
	*Docker Desktop automatically knows the ContainerPort from Dockerfile:EXPOSE 5000*		  
	`docker run -p 7010:7010 -p 5000:5000 weather-service:latest` **DockerEngine** runs fresh **Container** with new **ContainerImage**  
	Run `docker ps -a` List all.     
	**OR**  
	Docker.Desktop --> Images --> **weather-service** -->Play --> Optional Settings: Host Port: 5000 

	The Service is now running in browser at port 7010 and not at all for port 5000: 
	
	- [http://localhost:7010]()  - Homepage HTML
	- [http://localhost:7010/ping]()  - server alive test
	- [http://localhost:7010/weather?latitude=53.4&longitude=-2.15]()  Weather JSON Response

**NOTE:** The Docker service now continues to run in the background when all prompts are closed.  
*See: Docker.Desktop --> Containers / Images.*  
 - **ContainerImages** persist across reboot.  
 - **Containers** stop on reboot.   
 
	
## 6. Use Weather Client to connect to WeatherService Docker Container
With Docker Container running:  
- Visual Studio --> Startup Project --> WeatherApi *(no longer also start WeatherService)* --> Play  
  *(The WeatherClient could also be run in a Docker!)*
- The client will open a browser at: [https://localhost:7034/]() showing the HTML Homepage.  
- The GetWeather form on the homepage will call gRPC on port 5000 to the Docker server and return a JSON result.
- HTTP REST endpoint also works: [https://localhost:7034/weather?latitude=53.41&longitude=-2.15]()


## 7. Azure Container Apps setup

### Step 0 — First we need to create ACR and ACA on Azure

### Step 1 — Push your image to Azure Container Registry (ACR)

Tag your local image:
`docker tag weather-service:latest <youracr>.azurecr.io/weather-service:latest`
Push it to ?
`docker push <youracr>.azurecr.io/weather-service:latest`
This publishes your container image so Azure Container Apps can pull it.

### Step 2 — Create an Azure Container App (ACA) using your ACR image
Configure the Container App to use your ACR image:  
 - Registry: your ACR  
 - ContainerImage:`<youracr>.azurecr.io/weather-service:latest`

ACA will:  

	- pull your image
	- run it inside a Linux container
	- expose port 5000 via HTTPS
	- optionally expose REST on 7010 internally
	- handle scaling, revisions, logs, metrics

### Step 3 — Configure Ingress
Set ingress to match your gRPC port:

	 - Ingress port: 5000
	 - Transport: HTTP/2
	 - Ingress type: External (public)

ACA maps:

`https://<your-app-name>.<region>.azurecontainerapps.io → container port 5000`

Your gRPC client connects to the ACA URL without specifying a port.

Step 4 — Set environment variables in ACA
Production configuration goes in ACA, not in your Dockerfile.

Examples:
```
ASPNETCORE_ENVIRONMENT = Production
OPEN_METEO_URL = https://api.open-meteo.com/v1/forecast
```
ACA injects these at runtime.

### Step 5 — View Logs
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
draw.io paths & layout, the devops wiki page, the 2 index.html files
add ping servier is alive to the draw.io
add docker contianer endine images to draw.io