FROM mcr.microsoft.com/dotnet/sdk:8.0 AS installer-env


COPY . /src/RuneFunctions
RUN cd /src/RuneFunctions && \
mkdir -p /home/site/wwwroot && \
dotnet publish *.csproj --output /home/site/wwwroot

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0-appservice
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true


### Puppeteer
RUN apt-get update && apt-get -f install && apt-get -y install wget gnupg2 apt-utils
RUN wget -q -O - https://dl.google.com/linux/linux_signing_key.pub | apt-key add -
RUN echo 'deb [arch=amd64] http://dl.google.com/linux/chrome/deb/ stable main' >> /etc/apt/sources.list
RUN apt-get update \
    && apt-get install -y google-chrome-stable --no-install-recommends --allow-downgrades \
    fonts-ipafont-gothic fonts-wqy-zenhei fonts-thai-tlwg fonts-kacst fonts-freefont-ttf 
    ###

COPY --from=installer-env ["/home/site/wwwroot", "/home/site/wwwroot"]
EXPOSE 80
EXPOSE 8080