#!/bin/bash

resource="https://management.azure.com/"

msiVersion="2018-02-01"

access_token="$(curl --silent --get \
    --url "http://169.254.169.254/metadata/identity/oauth2/token" \
    --data-urlencode "api-version=${imdsVersion}" \
    --data-urlencode "resource=${resource}" \
    --header "Metadata: true" \
    | jq -r ".access_token")"

imdsVersion="2021-02-01"

subscriptionId="$(curl --silent --get \
    --url "http://169.254.169.254/metadata/instance" \
    --data-urlencode "api-version=${imdsVersion}" \
    --header "Metadata: true" \
    | jq -r ".compute.subscriptionId" )"

resourceGroup="$(curl --silent --get \
    --url "http://169.254.169.254/metadata/instance" \
    --data-urlencode "api-version=${imdsVersion}" \
    --header "Metadata: true" \
    | jq -r ".compute.resourceGroupName" )"

vmName="$(curl --silent --get \
    --url "http://169.254.169.254/metadata/instance" \
    --data-urlencode "api-version=${imdsVersion}" \
    --header "Metadata: true" \
    | jq -r ".compute.name" )"

virtualMachineARMVersion="2021-03-01"

#
# Properly deallocate
#
curl --silent --request POST \
  --header "Authorization: Bearer ${access_token}" \
  --url "https://management.azure.com/subscriptions/${subscriptionId}/resourceGroups/${resourceGroup}/providers/Microsoft.Compute/virtualMachines/${vmName}/deallocate?api-version=${virtualMachineARMVersion}" \
  --data ""
