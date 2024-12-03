```plaintext
 ____  _     _        _ _           _           _   _____ _ _        ____            _                 
|  _ \(_)___| |_ _ __(_) |__  _   _| |_ ___  __| | |  ___(_) | ___  / ___| _   _ ___| |_ ___ _ __ ___  
| | | | / __| __| '__| | '_ \| | | | __/ _ \/ _` | | |_  | | |/ _ \ \___ \| | | / __| __/ _ \ '_ ` _ \ 
| |_| | \__ \ |_| |  | | |_) | |_| | ||  __/ (_| | |  _| | | |  __/  ___) | |_| \__ \ ||  __/ | | | | |
|____/|_|___/\__|_|  |_|_.__/ \__,_|\__\___|\__,_| |_|   |_|_|\___| |____/ \__, |___/\__\___|_| |_| |_|
                                                                           |___/                       
```

- [Architecture](#architecture)📐
- [Client Node](#client-node)👨‍💼
  - [Functionality](#functionality)
- [Master Node](#master-node)👑
  - [RPC Methods](#rpc-methods)
- [Worker Node](#worker-node)👨‍🌾
  - [RPC Methods](#rpc-methods)
- [Prometheus](#prometheus)📈
  - [Alertmanager](#alertmanager)
  - [Grafana](#grafana)


The goal of this project was to create a redundant filing system that would allow a user to store
their data via gRPC. This architecture is developed using C# with both Windows and Linux compatibility.

## Architecture

![Screenshot 2024-12-01 141458](https://github.com/user-attachments/assets/76906582-e82a-47b8-979b-2c0abad3dbd8)

## Client Node

The client node is where the user will interact with the Master Node. Currently the project is setup to use a cli interface for user's
to interact with. Long term, the goal would be to develop a front end application that could interact with the C# backend to handle rpc
request/responses.

### Functionality

As an example as to how the user is able to interact with the client node to make requests is as follow.

1. Download the Release:
```plaintext
https://github.com/mattsel/Distributed-File-System/releases
```

2. Run the Start File (Batch/Bash)
```plaintext
PS C:\User\dev\DFS> .\start.bat
```

4. Use the CLI
```plaintext
PS C:\User\dev\Distributed-File-System> DFS CreateNode --workerAddress "https://localhost:5003"
PS C:\User\dev\Distributed-File-System> DFS DeleteNode --workerAddress "https://localhost:5003"
PS C:\User\dev\Distributed-File-System> DFS SingleStore --file-path "C:\User\dev\file.txt"
PS C:\User\dev\Distributed-File-System> DFS ChunkLocation --file-path "file.txt"
PS C:\User\dev\Distributed-File-System> DFS ListFiles
PS C:\User\dev\Distributed-File-System> DFS GetWorkerResources --workerAddress "https://localhost:5003"
PS C:\User\dev\Distributed-File-System> DFS DistributeFile --file-path "C:\User\dev\file.txt"
PS C:\User\dev\Distributed-File-System> DFS RetrieveFile --file-path "file.txt"
PS C:\User\dev\Distributed-File-System> DFS DeleteFile --file-path "file.txt"
```

## Master Node

The Master Node is responsible to act as the middleware for communication between the Client Node and the Worker Nodes. Much of the Master Node's memory
is stored using MongoDB. This design decision was made to help better the redundancy in the system if the network were to be shutdown, it's memory would not be lost.
This was also done to reduce latency and resource governance of the network.

### RPC Methods

#### CreateNode

- **Description**: Creates a new Worker Node in the network

##### Request: `CreateNodeRequest`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `workerAddress` | string | The address of the worker node to be created |

##### Response: `CreateNodeResponse`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `status`        | bool   | The result of the operation's successfulness |
| `message`       | string | Additional information about the status      |

---

#### DeleteNode

- **Description**: Deletes a new Worker Node in the network

##### Request: `DeleteNodeRequest`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `workerAddress` | string | The address of the worker node to be deleted |

##### Response: `DeleteNodeResponse`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `status`        | bool   | The result of the operation's successfulness |
| `message`       | string | Additional information about the status      |

---

#### SingleStore

- **Description**: Stores data to a single Worker Node

##### Request: `SingleStoreRequest`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `fileName`      | string | The name of the file that is being stored    |
| `chunkData`     | bytes  | The data to be stored on the worker node     |
| `chunkSize`     | int64  | The size of the data chunk being stored      |

##### Response: `SingleStoreResponse`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `status`        | bool   | The result of the operation's successfulness |
| `message`       | string | Additional information about the status      |

---

#### ChunkLocations

- **Description**: Returns a list of Worker Nodes that are storing a particular file

##### Request: `ChunkLocationsRequest`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `fileName`      | string | The name of the file that is being queried   |

##### Response: `ChunkLocationsResponse`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `status`        | bool   | The result of the operation's successfulness |
| `message`       | string | Additional information about the status      |
| `workerAddress` | string | List of worker addresses that store the file |
| `chunkId`       | string | List of chunkIds that corrospond to the file |

---

#### ListFiles

- **Description**: Returns a list of all files in the network

##### Request: `ListFilesRequest`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| (No filed)      | N/A    | No parameters are requried to retrive files  |

##### Response: `ListFilesResponse`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `status`        | bool   | The result of the operation's successfulness |
| `message`       | string | Additional information about the status      |
| `fileName`      | string | List of files that are stored in the network |

---

#### GetWorkerResources

- **Description**: Returns the resources of a given worker such as CPU, Memory, or Disk Space

##### Request: `GetWorkerResourcesRequest`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `workerAddress` | string | The address of the worker to querey resources|

##### Response: `GetWorkerResourcesResponse`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `status`        | bool   | The result of the operation's successfulness |
| `message`       | string | Additional information about the status      |
| `cpuUsage`      | float  | The CPU usage percentage of the worker       |
| `memoryUsage`   | float  | The memory usage percentage of the worker    |
| `diskSpace`     | float  | The disk space usage percentage of the worker|

---

#### DistributeFile

- **Description**: Distributes a file to multiple worker nodes in the system

##### Request: `DistributeFileRequest`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `fileName`      | string | The name of the file to distribute           |
| `fileData`      | bytes  | The file data to distribute in bytes         |

##### Response: `DistributeFileResponse`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `status`        | bool   | The result of the operation's successfulness |
| `message`       | string | Additional information about the status      |

---

#### RetrieveFile

- **Description**: Returns the file data given a filename

##### Request: `RetrieveFileRequest`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `fileName`      | string | The name of the file to retrieve it's data   |

##### Response: `RetrieveFileResponse`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `status`        | bool   | The result of the operation's successfulness |
| `message`       | string | Additional information about the status      |
| `fileName`      | string | The name of the retrieved file               |
| `fileData`      | bytes  | The file data returned from various workers  |

---

#### DeleteFile

- **Description**: Deletes the file's data from all workers that are storing it's data

##### Request: `DeleteFileRequest`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `fileName`      | string | The name of the file to delete it's data     |

##### Response: `DeleteFileResponse`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `status`        | bool   | The result of the operation's successfulness |
| `message`       | string | Additional information about the status      |

---

## Worker Node

The Worker Node is responsible for doing all the work as directed by the Master Node. Their can be numerous worker's
in a network that will all be utilized by the Master Node's direction. Worker's can be both generated and deleted
using the Master Node's `CreateNode` or `DeleteNode` RPC calls.

### RPC Methods

#### StoreChunk

- **Description**: Will store a chunk given to it by the Master Node

##### Request: `StoreChunkRequest`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `chunkId`       | string | The unique chunkId associated with the file  |
| `chunkData`     | bytes  | The data that is used to be stored on disk   |

##### Response: `StoreChunkResponse`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `status`        | bool   | The result of the operation's successfulness |
| `message`       | string | Additional information about the status      |

---

#### GetChunk

- **Description**: Retrieves the chunk's data from the Worker Node's disk

##### Request: `GetChunkRequest`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `chunkId`       | string | The unique chunkId associated with the file  |

##### Response: `GetChunkRespone`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `status`        | bool   | The result of the operation's successfulness |
| `message`       | string | Additional information about the status      |
| `chunkData`     | bytes  | The data from the chunkId that was provided  |

---

#### DeleteChunk

- **Description**: Deletes the chunk from the Worker's disk

##### Request: `DeleteChunkRequest`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `chunkId`       | string | The unique chunkId associated with the file  |

##### Response: `DeleteChunkResponse`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `status`        | bool   | The result of the operation's successfulness |
| `message`       | string | Additional information about the status      |

---

#### ResourceUsage

- **Description**: Retrieves the worker's resource usage CPU, Memory, and Disk

##### Request: `GetWorkerResourcesRequest`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `workerAddress` | string | The address of the worker to querey resources|

##### Response: `GetWorkerResourcesResponse`
| Field           | Type   | Description                                  |
|-----------------|--------|----------------------------------------------|
| `status`        | bool   | The result of the operation's successfulness |
| `message`       | string | Additional information about the status      |
| `cpuUsage`      | float  | The CPU usage percentage of the worker       |
| `memoryUsage`   | float  | The memory usage percentage of the worker    |
| `diskSpace`     | float  | The disk space usage percentage of the worker|

---

## Prometheus

Prometheus is a Time Series Database (TSDB) which is used to help both store and querey metrics from the application.
This technology was integrated into the Distributed File System in order to help with both system monitoring and 
overall auditing of the network. Each time a worker node is either created or deleted, it will begin exporting metrics
to it's respective endpoint `/metrics` which is then scraped to a singluar endpoint inside of the `prometheus.yml`.
The defualt scrape address for prometheus metrics is on port `9090`.

Example:
```yml
scrape_configs:
  - job_name: 'scrape_https://localhost:5001'
    scrape_interval: 15s
    scheme: https
    static_configs:
      - targets: ['localhost:5001']
    tls_config:
      insecure_skip_verify: true
```

### Alertmanager

Alertmanager is a powerful tool that allows Prometheus metrics to be monitored and assessed. Often time's
rules are described inside of an alert_rule.yml file, which define a set of conditions in which once met,
will trigger alertmanager to act as a contact to alert the client about the condition.

alert_rules.yml
```yml
rules:
# Alerts when a worker's CPU Usage is above 90%
  - alert: HighCPUUsage
    expr: worker_node_cpu_usage_percentage > 90  #90% CPU
    for: 5m
    labels:
      severity: critical
    annotations:
      summary: "High CPU usage on worker {{ $labels.worker }}"
      description: "CPU usage on worker {{ $labels.worker }} is above 90% for more than 5 minutes."

# Alerts when a worker's Memory Usage is above 8GB
  - alert: HighMemoryUsage
    expr: worker_node_memory_usage_bytes > 8589934592 # 8GB
    for: 5m
    labels:
      severity: warning
    annotations:
      summary: "High memory usage on worker {{ $labels.worker }}"
      description: "Memory usage on worker {{ $labels.worker }} is above 80GB for more than 5 minutes."

# Alerts when the worker's Disk Space is under 1 GB
  - alert: LowDiskSpace
    expr: worker_node_available_disk_space_bytes < 1073741824 # 1GB
    for: 10m
    labels:
      severity: critical
    annotations:
      summary: "Low disk space on worker {{ $labels.worker }}"
      description: "Available disk space on worker {{ $labels.worker }} is below 1GB for more than 10 minutes."
```

alertmanager.yml
```yml
# Where the alerts will be sent to via email.
receivers:
  - name: 'email-receiver'
    email_configs:
      - to: '${SMTP_TO}'
        from: '${SMTP_FROM}'
        smarthost: '${SMTP_SMARTHOST}'
        auth_username: '${SMTP_AUTH_USERNAME}'
        auth_password: '${SMTP_AUTH_PASSWORD}'
        require_tls: false
        send_resolved: true
```

### Grafana

Grafana is important because it allows the consumer to track their Prometheus metrics of a longer period of time.
Any of the metrics defined in the application be queried by simply adding the Prometheus metrics as a data source.
The most simple example is to scrape: `https://localhost:9090`.

![Screenshot 2024-12-01 195252](https://github.com/user-attachments/assets/4765477d-c549-4e08-b68b-4a936587e3aa)

Below are the metrics defined in the application that can be queried:
```csharp
GrpcCallsCounter = Metrics.CreateCounter(
    "master_node_grpc_calls_total",
    "Total number of gRPC calls received by the master node",
    new CounterConfiguration { LabelNames = new[] { "request" } }
);

ErrorCount = Metrics.CreateCounter(
    "master_node_errors_total",
    "Total number of errors encountered by the master node",
    new CounterConfiguration { LabelNames = new[] { "request" } }
);

RequestDuration = Metrics.CreateHistogram(
    "master_node_request_duration_seconds",
    "Histogram of request durations for the master node in seconds",
    new HistogramConfiguration { LabelNames = new[] { "request" } }
);

GrpcCallsCounter = Metrics.CreateCounter(
    "worker_node_grpc_calls_total",
    "Total number of gRPC calls received by the worker node",
    new CounterConfiguration { LabelNames = new[] { "request" } }
);

ErrorCount = Metrics.CreateCounter(
    "worker_node_errors_total",
    "Total number of errors encountered by the worker node",
    new CounterConfiguration { LabelNames = new[] { "request" } }
);

RequestDuration = Metrics.CreateHistogram(
    "worker_node_request_duration_seconds",
    "Histogram of request durations for the worker node in seconds",
    new HistogramConfiguration { LabelNames = new[] { "request" } }
);

CpuUsage = Metrics.CreateGauge(
    "worker_node_cpu_usage_percentage",
    "Current CPU usage percentage of the worker node",
    new GaugeConfiguration { LabelNames = new[] { "worker" } }
);

MemoryUsage = Metrics.CreateGauge(
    "worker_node_memory_usage_bytes",
    "Current memory usage (in bytes) of the worker node",
    new GaugeConfiguration { LabelNames = new[] { "worker" } }
);

AvailableDiskSpace = Metrics.CreateGauge(
    "worker_node_available_disk_space_bytes",
    "Current available disk space (in bytes) of the worker node",
    new GaugeConfiguration { LabelNames = new[] { "worker" } }
);
```

---
