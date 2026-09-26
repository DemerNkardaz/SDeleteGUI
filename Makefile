.PHONY: x64 arm publish debug run clean iss64 issarm installer

x64:
	dotnet publish -c Release -r win-x64

arm:
	dotnet publish -c Release -r win-arm64

publish:
	$(MAKE) x64 arm

debug:
	dotnet build -c Debug

run:
	dotnet run

clean:
	dotnet clean

iss64:
	iscc installer/installer.iss

issarm:
	iscc /DAppArchitecture=arm64 installer/installer.iss

installer:
	$(MAKE) iss64 issarm

bridge:
	$(MAKE) publish installer
