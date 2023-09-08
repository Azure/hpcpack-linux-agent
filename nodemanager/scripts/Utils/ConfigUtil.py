#
# Handler library for Linux IaaS
#
# Copyright 2014 Microsoft Corporation
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#
# Requires Python 2.7+


"""
JSON def:
nodemanager.json
{
    "TrustedCAFile":"/opt/hpcnodemanager/certs/{trustedCAFile}.pem",
    "HeartbeatUri":"https://{0}:40001/api/{hostname}/computenodereported",
    "MetricInstanceIdsUri":"https://{0}:40001/api/{hostname}/getinstanceids",
    "MetricUri":"",
    "RegisterUri":"https://{0}:40001/api/{hostname}/registerrequested",
    "CertificateChainFile":"/opt/hpcnodemanager/certs/{certificateChainFile}.crt",
    "PrivateKeyFile":"/opt/hpcnodemanager/certs/{PrivateKeyFile}.key",
    "ListeningUri":"https://0.0.0.0:40002",
    "NamingServiceUri":[
        "http://{headnode1}:8939/api/fabric/resolve/singleton/",
        "http://{headnode2}:8939/api/fabric/resolve/singleton/",
        "http://{headnode3}:8939/api/fabric/resolve/singleton/"
        ],
    "DefaultServiceName":"SchedulerStatefulService",
    "UdpMetricServiceName":"MonitoringStatefulService"
}
"""


import os
import json
import Utils.AgentUtil as agentUtil

ConfigFile = '/opt/hpcnodemanager/nodemanager.json'


class ConfigUtility:
    def __init__(self, log, error):
        self._log = log
        self._error = error
        self.try_parse_config()
        
    def log(self, message):
        self._log('[ConfigUtility]' + message)

    def error(self, message):
        self._error('[ConfigUtility]' + message)

    def try_parse_config(self):
        config = None
        if not os.path.isfile(ConfigFile):
            self.error("Unable to find config file: " + ConfigFile)
            return None
        ctxt = agentUtil.GetFileContents(ConfigFile)
        if ctxt == None :
            self.error("Unable to read " + ConfigFile)
        try:
            config = json.loads(ctxt)
        except:
            self._error('Unable to decode ' + ConfigFile)
            return None
        self._config = config
        return config

    def get_cluster_connectionstring(self):
        headnodes = []
        for uri in self._config["NamingServiceUri"]:
            start = uri.index('//')+2
            uri = uri[start:]
            end = uri.index(':')
            dnsname = uri[:end]
            headnodes.append(dnsname)
        return ','.join(headnodes)