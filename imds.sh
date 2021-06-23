#!/bin/bash

curl --silent --get \
    --url "http://169.254.169.254/metadata/instance" \
    --data-urlencode "api-version=2021-02-01" \
    --header "Metadata: true" \
    | jq -M "."
