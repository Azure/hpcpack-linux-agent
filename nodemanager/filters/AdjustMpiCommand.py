# python2, python3

import sys, json

j = json.load(sys.stdin)

commandLine = j['m_Item2'].get('commandLine')
mpiSource = j['m_Item2']['environmentVariables'].get('CCP_MPI_SOURCE')
if commandLine and mpiSource:
    if commandLine.startswith('mpirun ') or commandLine.startswith('mpiexec '):
        machineFileOption = '-machinefile $CCP_MPI_HOSTFILE'
        commandLineSplit = commandLine.split(' ')
        if mpiSource.endswith('/mpirun') or mpiSource.endswith('/mpiexec'):
            commandLineSplit[0] = mpiSource
        elif mpiSource.endswith('/'):
            commandLineSplit[0] = '{}{}'.format(mpiSource, commandLineSplit[0])
        elif mpiSource.endswith('/mpivars.sh'):
            commandLineSplit[0] = 'source {}; {}'.format(mpiSource, commandLineSplit[0])
        else:
            commandLineSplit[0] = '{}/{}'.format(mpiSource, commandLineSplit[0])
        commandLineSplit.insert(1, machineFileOption)
        commandLine = ' '.join(commandLineSplit)
        j['m_Item2']['commandLine'] = commandLine

print(json.dumps(j))