#!/bin/sh
PAGES_COUNT_IN_PDF=`pdfinfo "$1" | grep Pages: | sed -e "s/Pages: *\([0-9]*\)/\1/g"`
PIECES=($PAGES_COUNT_IN_PDF/10)
mod=($PAGES_COUNT_IN_PDF % 10)
if [ "$mod" -gt 0 ];then PIECES=$[$PIECES+1];fi
for ((i=0;i<PIECES;i++))
do
mutool clean -gggg "$1" $(printf "%04d" $PDF_FILE_NAME).pdf $(($i*10+1))-$((i*10+10))
PDF_FILE_NAME=$(($i+1))
done