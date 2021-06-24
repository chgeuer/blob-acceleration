# Install things

```bash
#!/bin/bash

https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu#2004-

wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb

sudo dpkg -i packages-microsoft-prod.deb

sudo apt-get update -y; \
  sudo apt-get install -y apt-transport-https && \
  sudo apt-get update -y && \
  sudo apt-get install -y dotnet-sdk-5.0

# install az
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
# install azcopy
curl -sL https://aka.ms/downloadazcopy-v10-linux -o azcopy.tgz && tar xvfz azcopy.tgz && sudo mv azcopy_linux_amd64_10.11.0/azcopy /usr/local/bin && rm -rf azcopy_linux_amd64_10.11.0

sudo apt-get install -y nload jq
```


```json
{"$schema":"http://schema.management.azure.com/schemas/2015-01-01/deploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{"location":{"type":"String"},"storageAccountName":{"type":"String"},"accountType":{"type":"String"},"kind":{"type":"String"},"accessTier":{"type":"String"},"minimumTlsVersion":{"type":"String"},"supportsHttpsTrafficOnly":{"type":"Bool"},"allowBlobPublicAccess":{"type":"Bool"},"allowSharedKeyAccess":{"type":"Bool"},"networkAclsBypass":{"type":"String"},"networkAclsDefaultAction":{"type":"String"},"isContainerRestoreEnabled":{"type":"Bool"},"isBlobSoftDeleteEnabled":{"type":"Bool"},"blobSoftDeleteRetentionDays":{"type":"Int"},"isContainerSoftDeleteEnabled":{"type":"Bool"},"containerSoftDeleteRetentionDays":{"type":"Int"},"changeFeed":{"type":"Bool"},"isVersioningEnabled":{"type":"Bool"},"isShareSoftDeleteEnabled":{"type":"Bool"},"shareSoftDeleteRetentionDays":{"type":"Int"}},"variables":{},"resources":[{"type":"Microsoft.Storage/storageAccounts","apiVersion":"2019-06-01","name":"[parameters('storageAccountName')]","location":"[parameters('location')]","dependsOn":[],"tags":{},"sku":{"name":"[parameters('accountType')]"},"kind":"[parameters('kind')]","properties":{"accessTier":"[parameters('accessTier')]","minimumTlsVersion":"[parameters('minimumTlsVersion')]","supportsHttpsTrafficOnly":"[parameters('supportsHttpsTrafficOnly')]","allowBlobPublicAccess":"[parameters('allowBlobPublicAccess')]","allowSharedKeyAccess":"[parameters('allowSharedKeyAccess')]","networkAcls":{"bypass":"[parameters('networkAclsBypass')]","defaultAction":"[parameters('networkAclsDefaultAction')]","ipRules":[]}}},{"type":"Microsoft.Storage/storageAccounts/blobServices","apiVersion":"2019-06-01","name":"[concat(parameters('storageAccountName'), '/default')]","dependsOn":["[concat('Microsoft.Storage/storageAccounts/', parameters('storageAccountName'))]"],"properties":{"restorePolicy":{"enabled":"[parameters('isContainerRestoreEnabled')]"},"deleteRetentionPolicy":{"enabled":"[parameters('isBlobSoftDeleteEnabled')]","days":"[parameters('blobSoftDeleteRetentionDays')]"},"containerDeleteRetentionPolicy":{"enabled":"[parameters('isContainerSoftDeleteEnabled')]","days":"[parameters('containerSoftDeleteRetentionDays')]"},"changeFeed":{"enabled":"[parameters('changeFeed')]"},"isVersioningEnabled":"[parameters('isVersioningEnabled')]"}},{"type":"Microsoft.Storage/storageAccounts/fileservices","apiVersion":"2019-06-01","name":"[concat(parameters('storageAccountName'), '/default')]","dependsOn":["[concat('Microsoft.Storage/storageAccounts/', parameters('storageAccountName'))]","[concat(concat('Microsoft.Storage/storageAccounts/', parameters('storageAccountName')), '/blobServices/default')]"],"properties":{"shareDeleteRetentionPolicy":{"enabled":"[parameters('isShareSoftDeleteEnabled')]","days":"[parameters('shareSoftDeleteRetentionDays')]"}}}],"outputs":{}}
```




```bash
#!/bin/bash

az login --identity
azcopy login --identity

storageAccountName="chgeuerperfne"
containerName="container1"

az storage container create --account-name "${storageAccountName}" --name "${containerName}" --public-access "off" 




blobName="1gb.randombin"

echo "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}"

# dd if=/dev/urandom bs=64M count=32 | azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --block-size-mb 1 --from-to PipeBlob 
# 1073741792 bytes (1.1 GB, 1.0 GiB) copied, 12.9828 s, 82.7 MB/s

dd if=/dev/zero bs=1G count=128 | azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --block-size-mb 1024 --from-to PipeBlob 
# 137438953472 bytes (137 GB, 128 GiB) copied, 174.742 s, 787 MB/s


# dd if=/dev/urandom bs=64M count=32 | azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --block-size-mb 128 --from-to PipeBlob 
# 1073741792 bytes (1.1 GB, 1.0 GiB) copied, 6.52223 s, 165 MB/s

# dd if=/dev/zero bs=64M count=16 | azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --block-size-mb 128 --from-to PipeBlob 
# 2147483648 bytes (2.1 GB, 2.0 GiB) copied, 2.78609 s, 771 MB/s


# dd if=/dev/zero bs=1G count=128 | azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --block-size-mb 128 --from-to PipeBlob 
# 137438953472 bytes (137 GB, 128 GiB) copied, 190.313 s, 722 MB/s

time azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --from-to BlobPipe > /dev/null 

dd if=/dev/urandom bs=64M count=32 | azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --from-to PipeBlob 

dd if=/dev/urandom bs=1G count=1 | azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --from-to PipeBlob 

openssl rand 1073741792 | azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --from-to PipeBlob 


tera="1099511627776"
openssl rand "${tera}" | azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --from-to PipeBlob 


time azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --from-to BlobPipe > zero.bin

az storage blob list  --auth-mode login --account-name "${storageAccountName}" --container-name "${containerName}" | jq -r ".[].name"

https://www.binarytides.com/linux-commands-monitor-network/
iptraf
nload
