#!/bin/bash

imdsVersion="2021-02-01"

curl --silent --get \
    --url "http://169.254.169.254/metadata/instance" \
    --data-urlencode "api-version=${imdsVersion}" \
    --header "Metadata: true" \
    | jq -M "."
