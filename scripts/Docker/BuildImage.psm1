$ErrorActionPreference = 'Stop'


Function New-Image {
	Param(
		[Parameter(Mandatory=$True)]
		[string]$BaseImageName,

		[Parameter(Mandatory=$True)]
		[string]$Tag,

        [Parameter(Mandatory=$True)]
		[string]$BuildContext
    )


    docker image build --build-arg BASE_IMAGE=$BaseImageName --tag $Tag --isolation 'hyperv' $BuildContext
}


Export-ModuleMember -Function New-Image



