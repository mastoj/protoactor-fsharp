FROM gcr.io/google-appengine/aspnetcore:2.0
ADD ./bin/Release/netcoreapp2.0/publish/ /app

WORKDIR /app

ENTRYPOINT [ "dotnet", "Node1.dll" ]
