set outfile [open rmsd_5.dat w];                                             
set nf [molinfo top get numframes]
set frame0 [atomselect top "protein and backbone and noh and not (resid 72 to 76)" frame 0]
# rmsd calculation loop
for {set i 1 } {$i < $nf } { incr i } {
    set sel [atomselect top "protein and backbone and noh and not (resid 72 to 76)" frame $i]
    $sel move [measure fit $sel $frame0]
    puts $outfile "[measure rmsd $sel $frame0]"
}
close $outfile
