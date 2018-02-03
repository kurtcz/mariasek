read_dom () {
    local IFS=\>
    read -d \< ENTITY CONTENT
    local ret=$?
    TAG_NAME=${ENTITY%% *}
    ATTRIBUTES=${ENTITY#* }
    if [ $ATTRIBUTES == $TAG_NAME ]; then
        ATTRIBUTES=""
    fi
    ATTRIBUTES=${ATTRIBUTES%%/}
    #echo tag $TAG_NAME...
    if [[ $TAG_NAME =~ !--.* ]]; then
        COMMENT=${ATTRIBUTES%--}
        TAG_NAME=""
        ATTRIBUTES=""
        #echo $COMMENT
    else
        COMMENT=""
    fi
    return $ret
}

parse_dom() {
    if [[ $TAG_NAME == "Hra" ]]; then
        if [[ $ATTRIBUTES =~ Typ\=\"([A-Za-z\ ]+)\" ]]; then
            GAME_TYPE_COMPLETE=${BASH_REMATCH[1]}
            GAME_TYPE_STR=$GAME_TYPE_COMPLETE
            GAME_TYPE=$GAME_TYPE_COMPLETE
            if [[ $GAME_TYPE == "Sedma Kilo" ]]; then
                GAME_TYPE_STR="Stosedm"
                GAME_TYPE="Kilo"
            elif [[ $GAME_TYPE == "Hra Sedma" ]] || [[ $GAME_TYPE == "Hra Sedma KiloProti" ]]; then
                GAME_TYPE_STR="Sedma"
                GAME_TYPE="Sedma"
            elif [[ $GAME_TYPE == "Hra SedmaProti" ]] || [[ $GAME_TYPE == "Hra KiloProti" ]] || [[ $GAME_TYPE == "Hra SedmaProti KiloProti" ]]; then
                GAME_TYPE_STR="Hra"
                GAME_TYPE="Hra"
            fi
            #echo Typ $GAME_TYPE \($GAME_TYPE_STR\)
        fi
        if [[ $ATTRIBUTES =~ Voli\=\"(Hrac[1-3]) ]]; then
            GAME_STARTER=${BASH_REMATCH[1]}
            #echo Voli $GAME_STARTER
        fi
    elif [[ ! -z $COMMENT ]] && [[ $CONFIDENCE == "-1" ]]; then
        #echo Komentar $COMMENT
        #zkus najit procenta v komentari 
        #nejdriv zkus hledat procenta posledni simulaci pred volbou
        #bash nepodporuje lookahead regex, musime ho nahradit pomoci while cyklu
        pattern="$GAME_TYPE\ \((.*)\).*Player\ [1-3]:\ $GAME_TYPE_STR"
        #echo $pattern
        SUBCOMMENT=$COMMENT
        local match_found=false
        while [[ $SUBCOMMENT =~ $pattern ]]; do
            match_found=true
            SUBCOMMENT=${BASH_REMATCH#* }
        done
        if [[ $match_found == true ]]; then
            SUBCOMMENT=${SUBCOMMENT%% *}
            if [[ $SUBCOMMENT =~ \((.*)\) ]]; then
                CONFIDENCE=${BASH_REMATCH[1]}
                #echo Simulace $CONFIDENCE
                #echo Jistota $((100*$CONFIDENCE))%
            fi
        else
            #potom zkus najit procenta v prvni simulaci po volbe
            pattern="$GAME_TYPE\ [[:alnum:]]+\ \(([^\)]*)\)"
            #echo $pattern
            if [[ $COMMENT =~ $pattern ]]; then
                CONFIDENCE=${BASH_REMATCH[1]}
                #echo Confidence $CONFIDENCE
            else
                pattern="$GAME_TYPE\ \(([^\)]*)\)"
                #echo $pattern
                if [[ $COMMENT =~ $pattern ]]; then
                    CONFIDENCE=${BASH_REMATCH[1]}
                    #echo Confidence $CONFIDENCE
                fi
            fi
        fi
    elif [[ $ATTRIBUTES =~ Zisk\=\"([0-9\-]+) ]]; then
        local money=${BASH_REMATCH[1]}
        if [[ $TAG_NAME == $GAME_STARTER ]]; then
            MONEY_WON=$money
            #echo Vyhra $MONEY_WON
        fi
    fi
}

GAME_TYPE="?"
GAME_TYPE_STR="?"
GAME_TYPE_COMPLETE="?"
GAME_STARTER="?"
CONFIDENCE="-1"
MONEY_WON="?"
while read_dom; do
    parse_dom
done < $1
if [[ $CONFIDENCE =~ /0$ ]]; then
    CONFIDENCE="-1"
fi
BUCKET=$((100 * $CONFIDENCE - 100 * $CONFIDENCE % 10))
echo ${1##*/},$GAME_TYPE_COMPLETE,$GAME_STARTER,$CONFIDENCE,$((100*$CONFIDENCE)),$BUCKET,$MONEY_WON