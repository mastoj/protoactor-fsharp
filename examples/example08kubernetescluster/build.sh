#!/bin/bash

dotnet publish -c Release Node1
dotnet publish -c Release Node2

docker build -t gcr.io/${PROJECT_ID}/protodemonode1:v1 Node1/.
docker build -t gcr.io/${PROJECT_ID}/protodemonode2:v1 Node2/.

gcloud docker -- push gcr.io/${PROJECT_ID}/protodemonode1:v1
gcloud docker -- push gcr.io/${PROJECT_ID}/protodemonode2:v1