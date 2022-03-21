#!/bin/bash

echo "Downloading fissaa executable file..."

if [ "$1" = linux ]; 
then
  wget https://fissaa-cli.s3.amazonaws.com/test/fissaa
  chmod +x fissaa
  ln -s "$PWD/fissaa" /usr/bin/fissaa
  echo "you can execute the console with: fissaa --help"
else
  wget https://fissaa-cli.s3.amazonaws.com/test/fissaa-win.exe
  echo "still not supported :("
fi
  

