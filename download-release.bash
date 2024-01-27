#!/usr/bin/env bash

# References:
# https://linuxize.com/post/bash-select/
# https://superuser.com/questions/944778/zip-extractor-reading-archives-from-stdin

RELEASE_DATE=2024-01-27
BASE_URL="https://github.com/MITRacecarNeo/RacecarSim-binary/archive/refs/tags"

select PLATFORM in windows mac linux
do
    TARGET="${BASE_URL}/${PLATFORM}_${RELEASE_DATE}.zip"
    #echo $TARGET
    #curl SELECTED_TARGET -o /tmp/tmp.zip && unzip /tmp/tmp.zip && rm /tmp/tmp.zip
    wget -qO- $TARGET | busybox unzip -
    break
done