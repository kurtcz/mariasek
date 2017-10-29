echo Filename,GameType,GameStarter,Simulations,Confidence,MoneyWon
for FILENAME in $1/*.end.hra; do
    ./Mariasek.Parser.sh $FILENAME
done