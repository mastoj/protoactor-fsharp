#!/bin/bash

dotnet publish -c Release app
#dotnet publish -c Release Node2
docker-compose up --build --scale worker=3
