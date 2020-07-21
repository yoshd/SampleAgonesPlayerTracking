FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS builder

WORKDIR /work

COPY . .

RUN dotnet publish -c Release


FROM mcr.microsoft.com/dotnet/core/runtime:3.1

WORKDIR /work

COPY --from=builder /work/bin/Release/netcoreapp3.1/publish .

ENTRYPOINT ["dotnet","SampleAgonesPlayerTracking.dll"]
