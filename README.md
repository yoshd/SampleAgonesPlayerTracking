# SampleAgonesPlayerTracking

Game Server sample on Agones written in C#

## Setup Agones on minikube

Enable PlayerTracking Feature Gate

```
helm repo add agones https://agones.dev/chart/stable
helm install my-release --set "agones.featureGates=PlayerTracking=true" --namespace agones-system agones/agones
```

## Build docker image and minikube cache add

```
docker build . -t sample-pt
minikube cache add sample-pt:latest
```

## Apply

```
kubectl apply -k k8s
```

## Simple allocate

```
kubectl apply -f k8s/game_server_allocation.yml
```

## Connect GameServer (TCP echo server)

```
kubectl get gs
nc $(minikube ip) ${GAME_SERVER_PORT}
```

## Log

```
stern sample-pt
```
