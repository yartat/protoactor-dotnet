protoc Protos.proto -I. -I.. --csharp_out=.   --csharp_opt=file_extension=.g.cs --grpc_out . --plugin=protoc-gen-grpc=%USERPROFILE%\.nuget\packages\Grpc.Tools\1.13.0\tools\windows_x64\grpc_csharp_plugin.exe