// Small public test fixture for strict compiler-IR validation.
// Synthetic paths, refs, and records only. It is not a copy of the private 101-item audit topology.
const SNAPSHOT='2026-09-12T09:39:41Z';
const UPDATE='2026-09-12T09:00:00Z';
const REPORT_REF='7'.repeat(40);
const TESTED_REF='8'.repeat(40);
const REPORT_DIGEST='9'.repeat(64);
const NATIVE_RULE='Only direct unresolved artifacts for the frozen planned phase become native blockers.';
const NOTE='Derived from canonical hard inputs, OR alternatives/guards, external inputs, predicates, typed context-only snapshots, and the frozen artifact registry. Do not edit this projection manually.';
const MANIFEST='artifact:120:source-manifest:accept';
const READ_ONLY='artifact:120:existing-policy-snapshot:accept';
const PRIMARY='artifact:143:runnable-payload:candidate';
const FALLBACK='artifact:144:alternate-payload:candidate';
const HISTORICAL='artifact:69:historical-unchanged-checkout:accept';
const ABSORBED_CALIBRATION='artifact:124:historical-calibration-record:candidate';
const ABSORBED_BASELINE='artifact:124:historical-thermal-bounded-baseline:candidate';
const CLOSED_124='2026-09-12T05:23:52Z';
const COMMENT_124='https://github.com/xrlab-dau/CHOOGuard/issues/124#issuecomment-5643741218';
const GROUP='runtime-choice';
const DIRECT_GUARD='source-access:140:candidate';
const BRANCH_GUARD='source-access:74:candidate:alternate';
const PREDICATE='predicate:140:independent-runner';
const WORKSPACE_FIELDS=['workspaceId','realCheckoutPath','volumeIdentity','branchRef','baseRef','selectedWritePaths','leaseOwner','fencingToken'];
const UNITY_FIELDS=['workspaceId','editorInstanceId','editorPid','editorProcessStart','generatedOutputRoots','projectSettingsPaths'];
const MEASUREMENT_FIELDS=['runId','outputRoot','hostResourceNamespace','processGroup','ports','devices','serviceRooms','leaseOwner','fencingToken'];
const workspaceContract=()=>({template:'workspace-files-v1',phases:['candidate','accept'],requiredFields:[...WORKSPACE_FIELDS],pathsFrom:'phaseWriteScopes',keyExpansion:'workspaceId + canonicalized real paths, including generated closure',failure:'reject overlap in same physical workspace; independent branches may prepare proposals, canonical integration still needs requiredLocks'});
const unityContract=()=>({template:'unity-editor-v1',phases:['candidate','accept'],requiredFields:[...UNITY_FIELDS],generatedOutputRoots:['Assets/Generated/T-74/'],identityKeys:['unity-workspace:{workspaceId}','editor-process:{hostProcessNamespace}:{editorPid}:{editorProcessStart}','fs-output:{realOutputPath}'],liveValues:null,rule:'Same checkout conflicts even if Editor PID differs. Different checkouts/Editors/outputs can run concurrently when static integration locks also disjoint.'});
const measurementContract=()=>({template:'measurement-run-v1',phases:['candidate','accept'],requiredFields:[...MEASUREMENT_FIELDS],identityKeys:['run-output:{realOutputRoot}','process:{hostResourceNamespace}:{pid}:{start}','port:{networkNamespace}:{protocol}:{bindAddress}:{port}','device:{physicalDeviceId}','room:{serviceEndpoint}:{roomId}'],liveValues:null,rule:'runId or source SHA alone never defines exclusivity; empty resource list means none used, not unknown; record actual shared ports/devices/processes.'});
const RESOURCE_MODEL={dynamicTemplates:[{id:'workspace-files-v1',canonicalization:'realpath + filesystem volume/inode identity, symlink aliases collapse; workspace identity is not source digest',permissions:'read/read allowed; any overlapping write rejected in same bound workspace'},{id:'unity-editor-v1',canonicalization:'checkout identity AND real Editor process identity AND every generated root; checkout-level mutex prevents two Editors writing one project'},{id:'measurement-run-v1',canonicalization:'output path, network namespace/protocol/address/port, actual devices, service room, process identity; compare actual resources not textual placeholder names'}]};

const DERIVATION_CONTRACT={
 version:1,
 canonicalAuthorities:[
  'issues[].hardPredecessors (including accessGuard)',
  'issues[].oneOfInputs[].alternatives (including additionalInputs/accessGuard)',
  'issues[].externalInputs',
  'issues[].guardPredicates',
  'issues[].contextOnlyInputs (non-executable context only)',
  'artifactRegistry records excluding consumers/contextConsumers',
  'issues[].phaseWriteScopes',
  'baseline/project1-items.json + baseline/captured-at.txt',
  'reviews/historical-artifact-source-note.md + frozen #69 receipt bytes/ref'
 ],
 derivedOnly:[
  'issues[].candidateInputs','issues[].acceptanceInputs','issues[].phaseStates','issues[].successors',
  'issues[].nativeBlockers','issues[].nativeBlockersByPhase','issues[].nativeBlockerProjection',
  'issues[].orGateProjection','issues[].predicateGateProjection','issues[].sourceAccessPredicates',
  'issues[].authoredInputResolutions','issues[].artifactOutputBindings (phase outputs plus typed read-only snapshots)',
  'artifactRegistry[].consumers','artifactRegistry[].contextConsumers','producedArtifacts','derivedSuccessors',
  'phaseGraph','nativeProjection'
 ],
 rule:'Derived fields are replace-only compiler output. Exact-set mismatch is an error; no manually patched cache has precedence.'
};

const guardPredicate={
 allSelectedPathsCoveredByManifest:true,
 accessibleSourceRefRequired:true,
 pathDigestMatchRequired:true,
 localPresenceAloneQualifies:false,
 unavailableOrUnlistedPathBlocksOnlyThisConsumingPhase:true
};
const directGuard={
 id:DIRECT_GUARD,
 kind:'selected_path_source_access',
 activation:{type:'phase',phase:'candidate'},
 consumerPhase:'candidate',
 manifestProviderArtifact:MANIFEST,
 operations:[{path:'scripts/run-fixture.mjs',operation:'read_write',declaredAvailability:'local_unpublished',observedAt:SNAPSHOT,localAtCapture:true,developAtCapture:false}],
 predicate:guardPredicate
};
const branchGuard={
 id:BRANCH_GUARD,
 kind:'selected_path_source_access',
 activation:{type:'selected_or_alternative',group:GROUP,selectedArtifact:FALLBACK},
 consumerPhase:'candidate',
 manifestProviderArtifact:MANIFEST,
 operations:[{path:'scripts/alternate-fixture.mjs',operation:'read',declaredAvailability:'local_unpublished',observedAt:SNAPSHOT,localAtCapture:true,developAtCapture:false}],
 predicate:guardPredicate
};

const emptyBindings=()=>({candidate:[],accept:[],readOnlySnapshots:[]});
const captured=(value='Ready')=>({value,observedAt:value===null?null:SNAPSHOT,sourceSnapshotCapturedAt:SNAPSHOT,sourceIssueUpdatedAt:UPDATE,source:'Synthetic public compiler fixture'});
const planned=(phase='candidate')=>({phase,kind:phase===null?'none':'frozen_topology_planned_eligibility_not_live_claim',snapshotAt:SNAPSHOT,requires:'Recheck exact gates before execution.',liveStateClaim:false});
const preparation=(value=true)=>({value,basis:value?'Synthetic bounded preparation only.':'No Project row; preparation is not inferred.'});
function baseIssue(number,{status='Ready',plannedPhase='candidate',preparationAvailable=true}={}){
 return {
  number,
  hardPredecessors:[],oneOfInputs:[],externalInputs:[],guardPredicates:[],contextOnlyInputs:[],
  phaseWriteScopes:{prepare:[],candidate:[{path:`docs/resource/T-${number}.md`,mode:'isolated_proposal',physicalBinding:`workspace-file:{workspaceId}:docs/resource/T-${number}.md`,canonicalWriteAllowed:false}],accept:[{path:`docs/resource/T-${number}.md`,mode:'isolated_proposal',physicalBinding:`workspace-file:{workspaceId}:docs/resource/T-${number}.md`,canonicalWriteAllowed:false}]},readOnlyPaths:[],candidateInputs:[],acceptanceInputs:[],
  sourceAccessPredicates:[],artifactOutputBindings:emptyBindings(),nativeBlockers:[],
  nativeBlockersByPhase:{candidate:[],accept:[]},orGateProjection:{candidate:[],accept:[]},
  predicateGateProjection:{candidate:[],accept:[]},nonNativeArtifactGatesByPhase:{candidate:[],accept:[]},
  successors:[],authoredInputResolutions:[],resourceBindings:[workspaceContract()],
  board:{
   status,previouslyCapturedStatus:status,reason:status===null?'No Project row in the synthetic fixture.':'Synthetic captured Project status.',
   capturedProjectStatus:captured(status),capturedExecutionPhase:null,plannedExecutablePhase:plannedPhase,
   preparationAvailable:preparation(preparationAvailable),plannedExecutablePhaseEligibility:planned(plannedPhase),
   candidateInputs:[],acceptanceInputs:[],orInputs:[],externalInputs:{candidate:[],accept:[]},
   predicateInputs:{candidate:[],accept:[]},nativeBlockers:[],orGates:[],nonNativeArtifactGates:[],predicateGates:[]
  },
  statusProposal:status,statusReason:status===null?'No Project row in the synthetic fixture.':'Synthetic captured Project status.'
 };
}

const phaseRank=x=>({prepare:0,candidate:1,accept:2}[x]??99);
const unique=(values,key)=>{const seen=new Set();return values.filter(value=>{const k=key(value);if(seen.has(k))return false;seen.add(k);return true;});};
const sortStrings=values=>[...new Set(values)].sort((a,b)=>a.localeCompare(b));
function order(nodes,edges){
 const indegree=new Map(nodes.map(x=>[x,0])),out=new Map(nodes.map(x=>[x,[]]));
 for(const [from,to] of edges){out.get(from).push(to);indegree.set(to,indegree.get(to)+1);}
 const ready=nodes.filter(x=>indegree.get(x)===0).sort(),result=[];
 while(ready.length){const current=ready.shift();result.push(current);for(const next of out.get(current).sort()){indegree.set(next,indegree.get(next)-1);if(indegree.get(next)===0){ready.push(next);ready.sort();}}}
 return result;
}
function projection(issue,phase,registry,issueNumbers){
 const blockers=[],orGates=[],predicateGates=[];
 for(const edge of issue.hardPredecessors.filter(x=>x.consumerPhase===phase)){
  const artifact=registry.get(edge.artifact);
  if(!(artifact.satisfied===true||artifact.state==='qualified')&&Number.isInteger(artifact.producerIssue)&&issueNumbers.has(artifact.producerIssue))blockers.push({issue:artifact.producerIssue,artifact:artifact.id,producerPhase:artifact.producerPhase,consumerPhase:phase,reason:edge.reason,qualified:false,direct:true,...(edge.accessGuard?{guardId:edge.accessGuard.id,selectedPaths:edge.accessGuard.operations.map(x=>x.path)}:{})});
 }
 for(const group of issue.oneOfInputs.filter(x=>x.consumerPhase===phase))orGates.push({group:group.id,phase,status:'selection_required',selectedArtifact:null,alternativeArtifacts:group.alternatives.map(x=>x.artifact),rule:'No producer issue edge is emitted until explicit selection; alternatives are not AND-ed.'});
 for(const gate of issue.guardPredicates.filter(x=>x.consumerPhase===phase&&x.state!=='satisfied'))predicateGates.push({id:gate.id,phase,kind:gate.kind,state:gate.state,required:gate.required,nativeIssueEdge:false});
 return {blockers,orGates,nonNativeArtifactGates:[],predicateGates};
}
function phaseStates(issue,candidate,accept){
 const capturedStatus=issue.board.capturedProjectStatus.value,plannedPhase=issue.board.plannedExecutablePhaseEligibility.phase,preparationValue=issue.board.preparationAvailable.value;
 return {
  prepare:{status:preparationValue?'bounded_preparation_available_not_execution':'not_inferred',evidence:'No new preparation or execution completed by this topology audit.'},
  candidate:{status:plannedPhase===null?'not_evaluated_no_planned_phase':(candidate.blockers.length||candidate.orGates.some(x=>x.status!=='selected_eligible')||candidate.predicateGates.length?'frozen_preclaim_requirements_unmet':'eligible_only_after_live_claim_and_resource_check'),inputs:sortStrings(issue.hardPredecessors.filter(x=>x.consumerPhase==='candidate').map(x=>x.artifact)),orGroups:issue.oneOfInputs.filter(x=>x.consumerPhase==='candidate').map(x=>x.id),externalInputs:[],predicateInputs:sortStrings(issue.guardPredicates.filter(x=>x.consumerPhase==='candidate').map(x=>x.id))},
  accept:{status:plannedPhase===null?'not_evaluated_no_planned_phase':'not_verified',inputs:sortStrings(issue.hardPredecessors.filter(x=>x.consumerPhase==='accept').map(x=>x.artifact)),orGroups:issue.oneOfInputs.filter(x=>x.consumerPhase==='accept').map(x=>x.id),externalInputs:[],predicateInputs:sortStrings(issue.guardPredicates.filter(x=>x.consumerPhase==='accept').map(x=>x.id))},
  capturedExecutionState:{projectStatus:capturedStatus,observedAt:issue.board.capturedProjectStatus.observedAt,sourceSnapshotCapturedAt:SNAPSHOT,sourceIssueUpdatedAt:UPDATE,phase:null},
  preparationAvailable:structuredClone(issue.board.preparationAvailable),
  plannedExecutablePhaseEligibility:structuredClone(issue.board.plannedExecutablePhaseEligibility)
 };
}
function outputBindings(issue,artifacts){
 const result=emptyBindings();
 for(const artifact of artifacts.filter(x=>x.producerIssue===issue.number)){
  const scopes=issue.phaseWriteScopes[artifact.producerPhase]??[],scope=scopes.find(x=>x.path===artifact.path);
  if(scope)result[artifact.producerPhase].push({artifact:artifact.id,path:artifact.path,pathKind:artifact.pathKind,bindingKind:artifact.proposed===false?'requalifiable_phase_output':'phase_output',scopePath:scope.path,scopeMode:scope.mode,lockId:scope.physicalBinding,canonicalWriteAllowed:scope.canonicalWriteAllowed===true,rawReceiptPolicy:artifact.immutableReceiptIndex?.overwritePolicy??null});
  else result.readOnlySnapshots.push({artifact:artifact.id,path:artifact.path,pathKind:artifact.pathKind,bindingKind:'existing_read_only_snapshot',scopePath:artifact.path,scopeMode:'read_only',lockId:null,canonicalWriteAllowed:false,rawReceiptPolicy:null});
 }
 for(const phase of ['candidate','accept'])result[phase].sort((a,b)=>a.artifact.localeCompare(b.artifact));
 result.readOnlySnapshots.sort((a,b)=>a.artifact.localeCompare(b.artifact));
 return result;
}

export function round4CompilerSources(){
 return {records:[74,120,143,144,146].map(number=>({number,inputs:[]})).concat([{number:140,inputs:[{id:'source-manifest',fromIssue:120,qualification:'Exact synthetic source manifest'}]}]),annotations:[]};
}

export function makeRound4CompilerFixture(){
 const i74=baseIssue(74),i120=baseIssue(120),i140=baseIssue(140),i143=baseIssue(143),i144=baseIssue(144),i146=baseIssue(146,{status:null,plannedPhase:null,preparationAvailable:false});
 i140.hardPredecessors=[{issue:120,artifact:MANIFEST,producerPhase:'accept',consumerPhase:'candidate',reason:'Exact source manifest gates the synthetic runnable input.',accessGuard:structuredClone(directGuard)}];
 i140.guardPredicates=[{id:PREDICATE,kind:'independent_executor',consumerPhase:'candidate',state:'unverified',required:'Independent runner identity must be verified.',nativeIssueEdge:false}];
 i74.oneOfInputs=[{id:GROUP,consumerPhase:'candidate',qualification:'Select one compatible synthetic payload.',alternatives:[{issue:143,artifact:PRIMARY,producerPhase:'candidate'},{issue:144,artifact:FALLBACK,producerPhase:'candidate',additionalInputs:[{artifact:MANIFEST,consumerPhase:'candidate',reason:'The alternate branch requires exact source access.',accessGuard:structuredClone(branchGuard)}]}]}];
 i74.phaseWriteScopes.prepare=[{path:'{proposalRoot}/T-74/',mode:'isolated_proposal',physicalBinding:'output:{proposalRoot}/T-74/',canonicalWriteAllowed:false,requiresRegistration:true}];
 i74.resourceBindings.push(unityContract(),measurementContract());
 i120.phaseWriteScopes.accept=[{path:'docs/evidence/source-manifest.json',mode:'exclusive',physicalBinding:'repo-path:xrlab-dau/CHOOGuard:docs/evidence/source-manifest.json',canonicalWriteAllowed:true}];
 i120.readOnlyPaths=['docs/policy/existing.md'];
 i143.phaseWriteScopes.candidate=[{path:'build/primary.bin',mode:'isolated_proposal',physicalBinding:'workspace-file:{workspaceId}:build/primary.bin',canonicalWriteAllowed:false}];
 i144.phaseWriteScopes.candidate=[{path:'build/alternate.bin',mode:'isolated_proposal',physicalBinding:'workspace-file:{workspaceId}:build/alternate.bin',canonicalWriteAllowed:false}];
 i140.contextOnlyInputs=[{artifact:ABSORBED_CALIBRATION,issue:124,historicalSourceIssue:124,readWhen:'prepare',kind:'absorbed_historical_snapshot_context'}];
 i144.contextOnlyInputs=[{artifact:ABSORBED_BASELINE,issue:124,historicalSourceIssue:124,readWhen:'prepare',kind:'absorbed_historical_snapshot_context'}];
 const issues=[i74,i120,i140,i143,i144,i146];
 const artifacts=[
  {id:MANIFEST,producerIssue:120,producerPhase:'accept',path:'docs/evidence/source-manifest.json',pathKind:'immutable_receipt_index',proposed:true,state:'candidate',immutableReceiptIndex:{overwritePolicy:'Retain immutable raw receipts; replace this index only with a new manifest entry.'}},
  {id:READ_ONLY,producerIssue:120,producerPhase:'accept',path:'docs/policy/existing.md',pathKind:'file',proposed:false,state:'qualified'},
  {id:PRIMARY,producerIssue:143,producerPhase:'candidate',path:'build/primary.bin',pathKind:'file',proposed:true,state:'candidate'},
  {id:FALLBACK,producerIssue:144,producerPhase:'candidate',path:'build/alternate.bin',pathKind:'file',proposed:true,state:'candidate'},
  {id:HISTORICAL,producerIssue:69,producerPhase:'accept',path:'docs/history/checkout.md',pathKind:'file',proposed:false,state:'qualified',sourceRef:REPORT_REF,outputDigest:REPORT_DIGEST,historicalSnapshot:{readOnly:true,executable:false,currentOutput:false,nativeIssueEdge:false,sourceIssue:69,testedInputCodeRef:TESTED_REF,reportPublicationRef:REPORT_REF,reportByteSha256:REPORT_DIGEST}},
  ...[
   {id:ABSORBED_CALIBRATION,path:'docs/evidence/foundation/2026-09-09-fire-core.json',consumer:140,digest:'1'.repeat(64)},
   {id:ABSORBED_BASELINE,path:'docs/evidence/foundation/2026-09-09-simulation-components.json',consumer:144,digest:'2'.repeat(64)}
  ].map(({id,path,consumer,digest})=>({id,producerIssue:null,producerPhase:'candidate',path,pathKind:'file',proposed:false,state:'local_unpublished_absorbed_context_unqualified',availability:{state:'local_unpublished_context_only',access:'local_unpublished',teamAccessible:false,sourceRef:null,capturedLocalDigest:digest,observedAt:SNAPSHOT,requiresAvailabilityArtifact:MANIFEST,sourceEvidence:['source/docs/physics/heat-smoke-model.md:19-36']},satisfied:false,role:'absorbed_historical_context_not_produced',historicalSourceIssue:124,producerKind:'historical_attribution_only_not_current_output',wholeIssueAcceptance:false,historicalAttribution:{sourceIssue:124,issueState:'CLOSED',issueStateReason:'NOT_PLANNED',closedAt:CLOSED_124,closureDisposition:'absorption_transfer',closureCommentUrl:COMMENT_124,dataRejected:false,completedProducerClaim:false,currentOutput:false,legacyProducerPhaseLabel:'candidate',legacyPhaseIsExecutionClaim:false,executable:false,existingLaboratoryScope:{calibrationHoldoutFormatAlreadyExisted:true,stecklerCases:3,juelichCases:13,totalCases:16,region13Accepted:false,duplicateMeasurementRequired:false},transferredRemainingScope:{fire13Region:122,crowd13Region:125}},contextConsumers:[{issue:consumer,readWhen:'prepare',kind:'absorbed_historical_snapshot_context',executable:false,acceptanceInput:false,nativeIssueEdge:false}]}))
 ];
 const registry=new Map(artifacts.map(x=>[x.id,x])),issueNumbers=new Set(issues.map(x=>x.number)),consumers=new Map(artifacts.map(x=>[x.id,[]]));
 const add=(artifact,value)=>consumers.get(artifact).push(value);
 for(const issue of issues){
  for(const edge of issue.hardPredecessors)add(edge.artifact,{issue:issue.number,consumerPhase:edge.consumerPhase,producerPhase:edge.producerPhase,edgeType:'requires',when:null,...(edge.accessGuard?{guardId:edge.accessGuard.id}:{})});
  for(const group of issue.oneOfInputs)for(const alternative of group.alternatives){add(alternative.artifact,{issue:issue.number,consumerPhase:group.consumerPhase,producerPhase:alternative.producerPhase,edgeType:'selected_OR',group:group.id});for(const additional of alternative.additionalInputs??[])add(additional.artifact,{issue:issue.number,consumerPhase:additional.consumerPhase,producerPhase:registry.get(additional.artifact).producerPhase,edgeType:'OR_branch_guard',group:group.id,selectedAlternative:alternative.artifact,guardId:additional.accessGuard.id});}
 }
 const consumerKey=x=>[x.issue,x.consumerPhase,x.producerPhase??'',x.edgeType,x.group??'',x.selectedAlternative??'',x.guardId??''].join('|');
 for(const artifact of artifacts)artifact.consumers=unique(consumers.get(artifact.id),consumerKey).sort((a,b)=>a.issue-b.issue||phaseRank(a.consumerPhase)-phaseRank(b.consumerPhase)||a.edgeType.localeCompare(b.edgeType)||String(a.group??'').localeCompare(String(b.group??''))||String(a.selectedAlternative??'').localeCompare(String(b.selectedAlternative??'')));
 for(const issue of issues){
  const candidate=projection(issue,'candidate',registry,issueNumbers),accept=projection(issue,'accept',registry,issueNumbers),plannedProjection=issue.board.plannedExecutablePhaseEligibility.phase==='candidate'?candidate:{blockers:[],orGates:[],nonNativeArtifactGates:[],predicateGates:[]};
  issue.candidateInputs=sortStrings(issue.hardPredecessors.filter(x=>x.consumerPhase==='candidate').map(x=>x.artifact));
  issue.acceptanceInputs=sortStrings(issue.hardPredecessors.filter(x=>x.consumerPhase==='accept').map(x=>x.artifact));
  issue.sourceAccessPredicates=[];
  for(const edge of issue.hardPredecessors)if(edge.accessGuard)issue.sourceAccessPredicates.push({...structuredClone(edge.accessGuard),dependencyArtifact:edge.artifact,edgeType:'requires'});
  for(const group of issue.oneOfInputs)for(const alternative of group.alternatives)for(const additional of alternative.additionalInputs??[])if(additional.accessGuard)issue.sourceAccessPredicates.push({...structuredClone(additional.accessGuard),dependencyArtifact:additional.artifact,edgeType:'OR_branch_guard',orGroup:group.id,selectedAlternative:alternative.artifact});
  issue.artifactOutputBindings=outputBindings(issue,artifacts);
  issue.nativeBlockersByPhase={candidate:candidate.blockers,accept:accept.blockers};issue.nativeBlockers=plannedProjection.blockers;
  issue.orGateProjection={candidate:candidate.orGates,accept:accept.orGates};issue.predicateGateProjection={candidate:candidate.predicateGates,accept:accept.predicateGates};issue.nonNativeArtifactGatesByPhase={candidate:[],accept:[]};
  issue.nativeBlockerProjection={computedFromSnapshotAt:SNAPSHOT,liveStateClaim:false,phase:issue.board.plannedExecutablePhaseEligibility.phase,preClaimVisibility:true,blockers:plannedProjection.blockers,orGates:plannedProjection.orGates,nonNativeArtifactGates:[],predicateGates:plannedProjection.predicateGates,futureAcceptanceInputs:issue.board.plannedExecutablePhaseEligibility.phase==='candidate'?accept.blockers:[],rule:NATIVE_RULE};
  issue.board.candidateInputs=issue.candidateInputs;issue.board.acceptanceInputs=issue.acceptanceInputs;issue.board.orInputs=issue.oneOfInputs.map(x=>({id:x.id,phase:x.consumerPhase,selected:null}));issue.board.predicateInputs={candidate:sortStrings(issue.guardPredicates.filter(x=>x.consumerPhase==='candidate').map(x=>x.id)),accept:[]};issue.board.nativeBlockers=plannedProjection.blockers;issue.board.orGates=plannedProjection.orGates;issue.board.predicateGates=plannedProjection.predicateGates;
  issue.phaseStates=phaseStates(issue,candidate,accept);
 }
 i140.authoredInputResolutions=[{originalId:'source-manifest',originalProducer:120,qualification:'Exact synthetic source manifest',resolution:[{artifact:MANIFEST,producerPhase:'accept',consumerPhase:'candidate',guardId:DIRECT_GUARD}],orGroups:[],externalArtifacts:[],predicateIds:[],contextArtifacts:[],contextOnly:false,note:NOTE}];
 const successors=new Map(issues.map(x=>[x.number,[]]));
 for(const artifact of artifacts)if(successors.has(artifact.producerIssue))for(const consumer of artifact.consumers)if(['requires','selected_OR','OR_branch_guard'].includes(consumer.edgeType))successors.get(artifact.producerIssue).push({issue:consumer.issue,artifact:artifact.id,producerPhase:artifact.producerPhase,consumerPhase:consumer.consumerPhase,conditional:consumer.edgeType!=='requires',...(consumer.group?{orGroup:consumer.group}:{}),...(consumer.selectedAlternative?{selectedAlternative:consumer.selectedAlternative}:{}),...(consumer.edgeType==='OR_branch_guard'?{branchGuard:true}:{}),...(consumer.guardId?{guardId:consumer.guardId}:{})});
 const successorKey=x=>[x.issue,x.artifact,x.producerPhase,x.consumerPhase,x.conditional,x.orGroup??'',x.selectedAlternative??'',x.branchGuard??false,x.guardId??''].join('|');
 for(const issue of issues)issue.successors=unique(successors.get(issue.number),successorKey).sort((a,b)=>a.issue-b.issue||phaseRank(a.consumerPhase)-phaseRank(b.consumerPhase)||a.artifact.localeCompare(b.artifact)||String(a.orGroup??'').localeCompare(String(b.orGroup??'')));
 const topology={sourceSnapshot:SNAPSHOT,resourceModel:structuredClone(RESOURCE_MODEL),derivationContract:DERIVATION_CONTRACT,projectionRules:{nativeBlockedBy:NATIVE_RULE},issues,artifactRegistry:artifacts,externalHistoricalRefs:[{number:69,state:'CLOSED',stateReason:'COMPLETED',scheduled:false,executable:false,sourceRef:REPORT_REF,testedInputCodeRef:TESTED_REF,reportPublicationRef:REPORT_REF,reportByteSha256:REPORT_DIGEST,acceptedArtifact:HISTORICAL},{number:124,state:'CLOSED',stateReason:'NOT_PLANNED',title:'Synthetic absorbed laboratory context',role:'external_historical_reference',scheduled:false,warning:'Context only; no current producer or completed execution is inferred.',kind:'absorbed_superseded_context',executable:false,closedAt:CLOSED_124,url:'https://github.com/xrlab-dau/CHOOGuard/issues/124',closureDisposition:'absorption_transfer',closureCommentUrl:COMMENT_124,dataRejected:false,acceptedAsCompleted:false,existingLaboratoryScope:{calibrationHoldoutFormatAlreadyExisted:true,stecklerCases:3,juelichCases:13,totalCases:16,region13Accepted:false,duplicateMeasurementRequired:false},transferredRemainingScope:{fire13Region:122,crowd13Region:125}}]};
 topology.derivedSuccessors=Object.fromEntries(issues.map(issue=>[String(issue.number),structuredClone(issue.successors)]));
 topology.producedArtifacts=Object.fromEntries(issues.map(issue=>[String(issue.number),artifacts.filter(x=>x.producerIssue===issue.number).map(x=>x.id)]));
 const phaseNodes=issues.flatMap(issue=>['prepare','candidate','accept'].map(phase=>issue.number+':'+phase)),artifactNodes=artifacts.map(x=>x.id),expandedNodes=[...phaseNodes,...artifactNodes];
 const internalEdges=issues.flatMap(issue=>[[issue.number+':prepare',issue.number+':candidate'],[issue.number+':candidate',issue.number+':accept']]),artifactEdges=[...internalEdges];
 for(const artifact of artifacts){if(issueNumbers.has(artifact.producerIssue))artifactEdges.push([artifact.producerIssue+':'+artifact.producerPhase,artifact.id]);if(artifact.availability?.requiresAvailabilityArtifact)artifactEdges.push([artifact.availability.requiresAvailabilityArtifact,artifact.id]);for(const consumer of artifact.consumers)artifactEdges.push([artifact.id,consumer.issue+':'+consumer.consumerPhase]);}
 const sortedArtifactEdges=unique(artifactEdges,x=>x.join('|')).sort((a,b)=>a[0].localeCompare(b[0])||a[1].localeCompare(b[1]));
 topology.phaseGraph={internalEdges,artifactEdges:sortedArtifactEdges,topologicalOrder:order(expandedNodes,sortedArtifactEdges),interpretation:'Round4 conservative phase/artifact union DAG. It includes all OR alternatives/branch guards only for cycle proof. It is not an all-accept native issue projection.'};
 const projections=new Map(issues.map(issue=>[issue.number,projection(issue,'candidate',registry,issueNumbers)])),frontier=[],issueEdges=[],orGates=[],nonNative=[];
 for(const issue of issues){if(issue.board.plannedExecutablePhaseEligibility.phase===null)continue;const current=projections.get(issue.number);frontier.push({issue:issue.number,phase:'candidate',capturedExecutionPhase:null,statusAtCapture:issue.board.status,blockerArtifacts:current.blockers.map(x=>x.artifact),orGroups:current.orGates.map(x=>x.group),nonNativeArtifacts:[],predicates:current.predicateGates.map(x=>x.id)});for(const blocker of current.blockers)issueEdges.push([blocker.issue,issue.number,blocker.artifact]);for(const gate of current.orGates)orGates.push({issue:issue.number,...gate});for(const gate of current.predicateGates)nonNative.push({issue:issue.number,...gate});}
 const sortedIssueEdges=unique(issueEdges,x=>x.join('|')).sort((a,b)=>a[0]-b[0]||a[1]-b[1]||a[2].localeCompare(b[2])),pairs=unique(sortedIssueEdges.map(([from,to])=>[String(from),String(to)]),x=>x.join('|'));
 topology.nativeProjection={kind:'frozen_evidence_filtered_direct_frontier',sourceSnapshotCapturedAt:SNAPSHOT,liveStateClaim:false,frontier:frontier.sort((a,b)=>a.issue-b.issue),issueEdges:sortedIssueEdges,unresolvedOrGates:orGates.sort((a,b)=>a.issue-b.issue||a.group.localeCompare(b.group)),nonNativeArtifactAndPredicateGates:nonNative.sort((a,b)=>a.issue-b.issue||String(a.id).localeCompare(String(b.id))),collapsedIssueDag:{nodes:issues.length,edges:pairs.length,acyclic:true,topologicalOrder:order(issues.map(x=>String(x.number)),pairs)},excludedProjection:'No hypothetical all-accept collapsed union is generated or publishable.'};
 return topology;
}
