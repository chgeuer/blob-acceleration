

storageAccountName="$( az storage account list | jq -r ".[0].name" )"
containerName="container1"

az storage container create --account-name "${storageAccountName}" --name "${containerName}" --public-access "off" 



azcopy login --identity

blobName="1gb.randombin"

echo "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}"


dd if=/dev/urandom bs=64M count=32 | azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --block-size-mb 1 --from-to PipeBlob 
# 1073741792 bytes (1.1 GB, 1.0 GiB) copied, 12.9828 s, 82.7 MB/s

dd if=/dev/zero bs=1G count=128 | azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --block-size-mb 1024 --from-to PipeBlob 
# 137438953472 bytes (137 GB, 128 GiB) copied, 174.742 s, 787 MB/s




dd if=/dev/urandom bs=64M count=32 | azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --block-size-mb 128 --from-to PipeBlob 
# 1073741792 bytes (1.1 GB, 1.0 GiB) copied, 6.52223 s, 165 MB/s

dd if=/dev/zero bs=64M count=16 | azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --block-size-mb 128 --from-to PipeBlob 
2147483648 bytes (2.1 GB, 2.0 GiB) copied, 2.78609 s, 771 MB/s


dd if=/dev/zero bs=1G count=128 | azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --block-size-mb 128 --from-to PipeBlob 
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
