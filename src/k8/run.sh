kubectl apply -f DistributedFileSystem.Client.yaml
kubectl apply -f DistributedFileSystem.Master.yaml
kubectl apply -f DistributedFileSystem.Worker.yaml
kubectl apply -f prometheus.yaml
kubectl apply -f prometheus-configmap.yaml
kubectl apply -f alertmanager-configmap.yaml
kubectl apply -f alertmanager.yaml