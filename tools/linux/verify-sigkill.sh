#!/bin/sh
set -eu
case "$1" in /storage/pkforge-full-test|/userdata/pkforge-full-test) ;; *) exit 2 ;; esac
cd "$1"
run="$1/crash-$(date +%Y%m%d-%H%M%S)"
mkdir "$run"
passed=0
for timing in before after; do
    for step in 0 1 2 3; do
        for recovery in complete restore; do
            fixture="$run/$timing-$step-$recovery"
            set +e
            ./device-tests/DeviceTests --kill-journal "$fixture" "$timing:$step" > "$run/kill-$timing-$step-$recovery.log" 2>&1
            result=$?
            set -e
            if [ "$result" -ne 137 ]; then cat "$run/kill-$timing-$step-$recovery.log"; echo "Expected SIGKILL (137), received $result"; exit 1; fi
            ./device-tests/DeviceTests --recover-killed-journal "$fixture" "$recovery"
            passed=$((passed + 1))
        done
    done
done
echo "ACTUAL SIGKILL SUITE: $passed/16 passed; two saves plus Bank, every publication boundary, completion and byte-exact restoration."
