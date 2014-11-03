#include "ProcessStartInfo.h"

ProcessStartInfo* ProcessStartInfo::FromJson(const web::json::value& jsonInfo)
{
    ProcessStartInfo *processStartInfo = new ProcessStartInfo();

	auto commandLineValue = jsonInfo.at(U("commandLine"));
	if (!commandLineValue.is_null() && commandLineValue.is_string()){
		processStartInfo->commandLine = commandLineValue.as_string();
		//std::cout << "Get commandLine from json : " << processStartInfo->commandLine << std::endl;
	}

	auto affinityValue = jsonInfo.at(U("affinity"));
	if (!affinityValue.is_null() && affinityValue.is_array()){
		auto& arr = affinityValue.as_array();
		for (auto i = arr.begin(); i != arr.end(); i++){
			processStartInfo->affinity.push_back(i->as_number().to_int64());
			//std::cout << "Get affinity from json : " << i->as_number().to_int64() << std::endl;
		}
	}

	auto workingDirectoryValue = jsonInfo.at(U("workingDirectory"));
	if (!workingDirectoryValue.is_null() && workingDirectoryValue.is_string()){
		processStartInfo->workingDirectory = workingDirectoryValue.as_string();
		//std::cout << "Get workingDirectory from json : " << processStartInfo->workingDirectory << std::endl;
	}

	auto environmentVariablesValue = jsonInfo.at(U("environmentVariables"));
	if (!environmentVariablesValue.is_null() && environmentVariablesValue.is_object()){
		auto& obj = environmentVariablesValue.as_object();
		for (auto i = obj.begin(); i != obj.end(); i++){
			std::string s = i->first + "=" + i->second.as_string();
			processStartInfo->environmentVariables.push_back(s);
			//std::cout << "Get environmentVariables from json : " << s << std::endl;
		}
	}

	//std::cout << "jsonInfo from json " << jsonInfo.serialize();

    return processStartInfo;
}

std::string ProcessStartInfo::GetCommand()
{
	std::string command = "";
	size_t index = commandLine.find_first_of(' ', 0);
	if (index != std::string::npos){
		command = commandLine.substr(0, index);
	}
	else {
		command = commandLine;
	}
	return command;
}

void ProcessStartInfo::GetCommandArgs(std::vector< std::string >* ret)
{
	std::cout << "GetCommandArgs : " << commandLine << std::endl;
	split(commandLine, ' ', ret);
}

void ProcessStartInfo::GetCommandEnvs(std::vector< std::string >* ret)
{
	//std::cout << "GetCommandEnvs : " << environmentVariables.size() << std::endl;

	//for (std::map<std::string, std::string>::iterator i = environmentVariables.begin(); i != environmentVariables.end(); ++i){
	//	std::cout << "Env :" + i->first + "=" + i->second << std::endl;
	//	ret->push_back(i->first + "=" + i->second);
	//}
}

void ProcessStartInfo::split(std::string& s, char delim, std::vector< std::string >* ret)
{
	size_t last = 0;
	size_t index = s.find_first_of(delim, last);
	while (index != std::string::npos)
	{
		ret->push_back(s.substr(last, index - last));
		last = index + 1;
		index = s.find_first_of(delim, last);
	}
	if (index - last>0)
	{
		ret->push_back(s.substr(last, index - last));
	}
}

std::string ProcessStartInfo::GetCommandScript()
{
	char scriptPath[256];

	strcpy(scriptPath, "/tmp/run.XXXXXX");
	/* Make temp file */
	if (mkstemp(scriptPath) < 0)
	{
		return "";
	}
	else
	{
		std::cout << "Script temp file name: " << scriptPath << std::endl;
	}

	commandScript = scriptPath;
	std::ofstream scriptFile(commandScript, std::ios::trunc);
	scriptFile << "#!/bin/bash" << std::endl << std::endl;

	if (workingDirectory != "" && access(workingDirectory.c_str(),0 ) != -1){
		scriptFile << "cd " << workingDirectory << std::endl << std::endl;
	}

	if (commandLine != ""){
		scriptFile << commandLine << std::endl;
	}

	scriptFile.close();

	return commandScript;
}

void ProcessStartInfo::DeleteCommandScript(){
	if (commandScript == ""){
		return;
	}

	unlink(commandScript.c_str());
	return;
}

