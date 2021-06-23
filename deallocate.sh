#!/bin/bash

access_token="$(curl --silent --get \
    --url "http://169.254.169.254/metadata/identity/oauth2/token" \
    --data-urlencode "api-version=2018-02-01" \
    --data-urlencode "resource=https://management.azure.com/" \
    --header "Metadata: true" \
    | jq -r ".access_token")"

vmId="$(curl --silent --get \
    --url "http://169.254.169.254/metadata/instance" \
    --data-urlencode "api-version=2021-02-01" \
    --header "Metadata: true" \
    | jq -r ".compute.resourceId" )"

curl --silent --request POST \
  --url "https://management.azure.com/${vmId}/deallocate?api-version=2021-03-01" \
  --header "Authorization: Bearer ${access_token}" \
  --data ""
