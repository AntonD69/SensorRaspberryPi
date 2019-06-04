SonarScanner.MSBuild.exe begin /k:"SensorRaspberryPi" /d:sonar.host.url="http://sonarqube.dev.devoops.co.za" /d:sonar.login="03c7a70d1d8221a1cab6a527aaae3848818ec795"
MsBuild.exe /t:Rebuild
SonarScanner.MSBuild.exe end /d:sonar.login="03c7a70d1d8221a1cab6a527aaae3848818ec795"