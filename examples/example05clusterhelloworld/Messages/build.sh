#!/bin/bash
protoc Protos.proto -I=. --csharp_out=. --csharp_opt=file_extension=.g.cs --plugin=protoc-gen-grpc=/Users/tomasjansson/.nuget/packages/grpc.tools/1.6.1/tools/macosx_x64/grpc_csharp_plugin
