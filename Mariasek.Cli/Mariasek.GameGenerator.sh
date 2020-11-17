gamesToGenerate=10
outputDir=out
gameType=""
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

echo Games to generate: $gamesToGenerate
echo Output directory: $outputDir
echo Game type: $gameType

start=`date "+%s"`

if [ ! -d $outputDir ]; then
  echo Creating directory $outputDir/
  mkdir $outputDir
fi

echo Generating $gamesToGenerate games to $outputDir/ ...
rm -rf $outputDir/*
i=1
while [ $i -le $gamesToGenerate ]; do
  if [ -z $gameType ]; then
    mono $tool > /dev/null
    filename=$(printf "%04d.def.hra" $i)
    cp -f ${path}_def.hra $outputDir/$filename
    filename=$(printf "%04d.end.hra" $i)
    cp -f ${path}_end.hra $outputDir/$filename
  else
    mono $tool -GameType=$gameType > /dev/null
    filename=$(printf "%04d-%s.def.hra" $i $gameType)
    cp -f ${path}_def.hra $outputDir/$filename
    filename=$(printf "%04d-%s.end.hra" $i $gameType)
    cp -f ${path}_end.hra $outputDir/$filename
  fi
  echo $filename saved to $outputDir/
  let i=i+1
done

end=`date "+%s"`
let time=end-start
printf "Finished in %s\n" $(showTime $time)