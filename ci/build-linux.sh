#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
cmake -S Omnilingua/Omnilingua.Cpp -B artifacts/cmake-sdk -DCMAKE_INSTALL_PREFIX="$PWD/artifacts/sdk"
cmake --install artifacts/cmake-sdk
cmake -S . -B artifacts/cpp-linux -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_PREFIX_PATH="$PWD/artifacts/sdk" -DOMNILINGUA_BUILD_BRIDGES=ON
cmake --build artifacts/cpp-linux --parallel
ctest --test-dir artifacts/cpp-linux --output-on-failure
cmake --install artifacts/cpp-linux --prefix "$PWD/artifacts/cpp-linux/plugins"
