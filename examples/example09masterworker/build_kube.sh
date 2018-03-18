#!/bin/bash

dotnet publish -c Release app
#dotnet publish -c Release Node2

docker build -t gcr.io/${PROJECT_ID}/fireproto:v1 app/.
##docker build -t gcr.io/${PROJECT_ID}/protodemonode2:v1 Node2/.

#gcloud docker -- push gcr.io/${PROJECT_ID}/f:v1
gcloud docker -- push gcr.io/${PROJECT_ID}/fireproto:v1