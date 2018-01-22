FROM gcr.io/google-appengine/aspnetcore:2.0
ADD ./bin/Release/netcoreapp2.0/publish/ /app

WORKDIR /app
EXPOSE 12000
ENTRYPOINT [ "dotnet", "app.dll" ]
#CMD [ "dotnet", "app.dll", "m", "100" ]
