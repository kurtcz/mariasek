inputDir=$1
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
#while [ $# -gt 0 ]; do
#  if [[ $1 == *"--"* ]]; then
#    v="${1/--/}"
#    declare $v="$2"
#  fi
#  shift
#done

echo Input directory: $inputDir

start=`date "+%s"`

rm -rf $inputDir/*.csv

for FILENAME in $1/*.end.hra; do
    echo Processing $FILENAME ...
    mono $tool hand2csv -filename=$FILENAME > /dev/null
done

end=`date "+%s"`
let time=end-start
echo Generated $inputDir/hand1.csv
printf "Finished in %s\n" $(showTime $time)