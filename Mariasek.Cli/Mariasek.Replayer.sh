inputDir="."
outputDir="."
path=bin/Debug/
tool=${path}Mariasek.Cli.exe

function showTime()
{
  let h=${1}/3600
  let m=(${1}%3600)/60
  let s=${1}%60
  printf "%02d:%02d:%02d\n" $h $m $s
}

#parse command line arguments into named variables
while [ $# -gt 0 ]; do
  if [[ $1 == *"--"* ]]; then
    v="${1/--/}"
    declare $v="$2"
  fi
  shift
done

echo Input directory: $inputDir
echo Output directory: $outputDir

start=`date "+%s"`

if [ ! -d $outputDir ]; then
  echo Creating directory $outputDir/
  mkdir $outputDir
fi

count=`ls -dq $inputDir/*.def.hra | wc -l`
echo Replaying $count games to $outputDir/ ...
rm -rf $outputDir/*
for filename in $inputDir/*.def.hra; do
  filename=${filename##*/}
  mono $tool load -filename="$inputDir/$filename" > /dev/null
  cp -f "$inputDir/$filename" "$outputDir/$filename"
  filename=${filename/def/end}
  cp -f "${path}_end.hra" "$outputDir/$filename"
  echo $filename saved to $outputDir/
done

end=`date "+%s"`
let time=end-start
printf "Finished in %s\n" $(showTime $time)