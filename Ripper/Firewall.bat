@echo off

fltmc >nul 2>&1 && (
	echo Configuration starting...
	echo Adding Firewall Rule...
	netsh advfirewall firewall add rule name="RIPPER (FIX)" dir=in action=block remoteip=173.194.55.0/24,206.111.0.0/16 enable=yes
	echo Rule added...
	echo Completed!
) || (
	echo Error: No administrator rights...
)

echo Closing in 5 seconds...
PING localhost -n 6 >NUL