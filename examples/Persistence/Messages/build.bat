..\..\..\tools\Grpc.Tools.1.0.1\tools\windows_x64\protoc.exe Protos.proto -I. -I..\..\..\src\ --csharp_out=.   --csharp_opt=file_extension=.g.cs --grpc_out . --plugin=protoc-gen-grpc=..\..\..\tools\Grpc.Tools\1.10.1\tools\windows_x64\grpc_csharp_plugin.exe