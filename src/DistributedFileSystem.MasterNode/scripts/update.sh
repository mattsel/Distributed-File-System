#!/bin/bash

address="$1"
action="$2"

echo "Received address: '$address'"
echo "Received action: '$action'"

if [[ -z "$address" ]]; then
    echo "Error: No address provided."
    exit 1
fi

if [[ "$action" != "CreateNode" && "$action" != "DeleteNode" ]]; then
    echo "Error: Invalid action specified. Use 'CreateNode' or 'DeleteNode'."
    exit 1
fi

configFilePath="../Prometheus/prometheus.yml"

if [[ ! -f "$configFilePath" ]]; then
    echo "Error: Configuration file does not exist at the specified path."
    exit 1
fi

add_scrape_config() {
    local address="$1"
    address="${address//https:\/\//}"
    local scrapeConfig="
  - job_name: 'scrape_$address'
    scrape_interval: 15s
    scheme: https
    static_configs:
      - targets: ['$address']
    tls_config:
      insecure_skip_verify: true
"

    if grep -q "scrape_$address" "$configFilePath"; then
        echo "Scrape config for address '$address' already exists in the configuration file."
    else
        echo "$scrapeConfig" >> "$configFilePath"
        echo "Scrape config for address '$address' has been added to the configuration file."
    fi
    curl -X POST $address/-/reload

}

remove_scrape_config() {
    local address="$1"
    local scrapeConfig="  - job_name: 'scrape_$address'
    scrape_interval: 15s
    scheme: https
    static_configs:
      - targets: ['$address']
    tls_config:
      insecure_skip_verify: true"

    if grep -qF "$scrapeConfig" "$configFilePath"; then
        sed -i.bak "/$scrapeConfig/d" "$configFilePath"
        echo "Scrape config for address '$address' has been removed from the configuration file."
    else
        echo "Scrape config for address '$address' does not exist in the configuration file."
    fi
}

if [[ "$action" == "CreateNode" ]]; then
    add_scrape_config "$address"
elif [[ "$action" == "DeleteNode" ]]; then
    remove_scrape_config "$address"
fi
