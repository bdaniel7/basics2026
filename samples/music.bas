10 REM Little tune (BEEP duration, pitch - semitones above middle C)
20 BEEP 0.3, 0
30 BEEP 0.3, 4
40 BEEP 0.3, 7
50 BEEP 0.5, 12
60 BEEP 0.3, 11
70 BEEP 0.3, 7
80 BEEP 0.8, 4
90 REM A rising sweep using SOUND (frequency in Hz)
100 FOR F = 200 TO 800 STEP 50
110 SOUND F, 0.1
120 NEXT F
130 PRINT "THE END"