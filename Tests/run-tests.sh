#!/usr/bin/env bash
# Runs Empire-CP's Tier-1 sandbox suites without RimWorld, Unity, Harmony or a game install —
# the same harness Core-MMF/Tests uses. Only the dependency-free logic is tested here; anything
# that needs the running game (the patches, the map overlay) is validated in-game via the
# [SYNAPSE-TEST] debug actions instead, and is not pretended to be covered here.
#
#   Ubuntu/WSL:  sudo apt-get install -y mono-mcs mono-runtime
#   Windows:     .NET SDK on PATH (dotnet)
#   Then:        Tests/run-tests.sh
set -u

cd "$(dirname "$0")/.." || exit 1

SRC=Source
OUT="${TMPDIR:-/tmp}/empirecp-tests"
mkdir -p "$OUT"

MCS_FLAGS="-langversion:latest -nowarn:0169,0414,0649,0219,0067"
failures=0

if command -v mcs > /dev/null 2>&1; then
    COMPILER=mcs
elif command -v dotnet > /dev/null 2>&1; then
    COMPILER=dotnet
else
    echo "Neither mcs (mono) nor dotnet (.NET SDK) is on PATH; cannot build the suites." >&2
    exit 1
fi

winpath() { if command -v cygpath > /dev/null 2>&1; then cygpath -m "$1"; else echo "$1"; fi; }

# run_suite <name> <files...> — build an executable with whichever compiler is available, then run it.
run_suite() {
    name=$1; shift

    if [ "$COMPILER" = mcs ]; then
        binary="$OUT/$name.exe"; rm -f "$binary"
        if ! mcs -target:exe $MCS_FLAGS -out:"$binary" "$@"; then
            echo "BUILD FAILED: $name"; failures=$((failures + 1)); return
        fi
        if ! mono "$binary"; then failures=$((failures + 1)); fi
        return
    fi

    proj="$OUT/$name"; rm -rf "$proj"; mkdir -p "$proj"
    {
        echo '<Project Sdk="Microsoft.NET.Sdk">'
        echo "  <PropertyGroup>"
        echo "    <OutputType>Exe</OutputType>"
        echo "    <TargetFramework>net8.0</TargetFramework>"
        echo "    <Nullable>disable</Nullable>"
        echo "    <LangVersion>latest</LangVersion>"
        echo "    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>"
        echo "    <NoWarn>0169;0414;0649;0219;0067</NoWarn>"
        echo "    <AssemblyName>$name</AssemblyName>"
        echo "  </PropertyGroup>"
        echo "  <ItemGroup>"
        for f in "$@"; do echo "    <Compile Include=\"$(winpath "$PWD/$f")\" />"; done
        echo "  </ItemGroup>"
        echo '</Project>'
    } > "$proj/$name.csproj"

    if ! dotnet build "$proj/$name.csproj" -v q --nologo -c Release -o "$proj/bin" > "$proj/build.log" 2>&1; then
        echo "BUILD FAILED: $name"; tail -12 "$proj/build.log"; failures=$((failures + 1)); return
    fi
    if ! dotnet "$proj/bin/$name.dll"; then failures=$((failures + 1)); fi
}

# The aggregator is pure and generic, so its suite compiles against nothing but itself.
run_suite aggregator \
    Tests/EmpireResourceAggregatorTests.cs \
    $SRC/EmpireResourceAggregator.cs

echo
if [ "$failures" -eq 0 ]; then
    echo "ALL SUITES PASSED"
    exit 0
fi

echo "$failures SUITE(S) FAILED"
exit 1
