.PHONY: release debug run clean

release:
	dotnet build -c Release

debug:
	dotnet build -c Debug

run:
	dotnet run

clean:
	dotnet clean
