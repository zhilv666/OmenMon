# OmenMon — build helper
# Usage:  make build | make rebuild | make clean | make run | make kill
# Run from the repository root (this directory) in Git Bash / MSYS.

MSBUILD ?= /c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/amd64/MSBuild.exe
PROPS   := -p:AssemblyVersion=0.0.0.0 -p:AssemblyVersionWord=Manual -p:Configuration=Release
FLAGS   := -v:minimal -nologo

.PHONY: build rebuild clean run kill

# Incremental build (default). Kills a running instance first so Bin\ is not locked.
build: kill
	"$(MSBUILD)" OmenMon.csproj -t:Build $(PROPS) $(FLAGS)

# Full clean + build
rebuild: kill
	"$(MSBUILD)" OmenMon.csproj -t:Clean,Build $(PROPS) $(FLAGS)

# Remove build outputs
clean: kill
	"$(MSBUILD)" OmenMon.csproj -t:Clean $(PROPS) $(FLAGS)

# Build then launch (needs elevation for fan control — run terminal as admin)
run: build
	./Bin/OmenMon.exe &

# Stop any running instance (ignore failure if none)
kill:
	-taskkill //F //IM OmenMon.exe 2>/dev/null || true
