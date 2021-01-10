dotnet publish Brutus/Brutus.csproj -r win-x86 -c Release -o ./publish/win-x86 /p:PublishSingleFile=true --self-contained false
dotnet publish Brutus/Brutus.csproj -r win-x64 -c Release -o ./publish/win-x64 /p:PublishSingleFile=true --self-contained false
dotnet publish Brutus/Brutus.csproj -r linux-x64 -c Release -o ./publish/linux-x64 /p:PublishSingleFile=true --self-contained false
dotnet publish Brutus/Brutus.csproj -r osx-x64 -c Release -o ./publish/osx-x64 /p:PublishSingleFile=true --self-contained false